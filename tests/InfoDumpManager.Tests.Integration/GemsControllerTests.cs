using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using InfoDumpManager.Application.Categories.Dtos;
using InfoDumpManager.Application.Common;
using InfoDumpManager.Application.GEMs.Dtos;
using InfoDumpManager.Tests.Integration.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace InfoDumpManager.Tests.Integration;

public sealed class GemsControllerTests : IAsyncLifetime
{
    static GemsControllerTests()
    {
        Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "true");
    }

    private readonly PostgreSqlTestcontainer _postgres;
    private CustomWebApplicationFactory? _factory;
    private HttpClient? _client;
    private string? _skipReason;

    public GemsControllerTests()
    {
        _postgres = new TestcontainersBuilder<PostgreSqlTestcontainer>()
            .WithDatabase(new PostgreSqlTestcontainerConfiguration
            {
                Database = "infodump",
                Username = "postgres",
                Password = "postgres"
            })
            .WithImage("postgres:16-alpine")
            .Build();
    }

    public async Task InitializeAsync()
    {
        try
        {
            await _postgres.StartAsync();
        }
        catch (InvalidOperationException ex)
        {
            _skipReason = $"Testcontainers unavailable: {ex.Message}";
            return;
        }

        _factory = new CustomWebApplicationFactory(_postgres);
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task PostGem_ValidInput_CreatesGem()
    {
        if (TrySkipTest())
        {
            return;
        }

        var gem = await CreateGemAsync("https://example.com", "Example GEM");

        gem.Id.Should().NotBeEmpty();
        gem.Title.Should().Be("Example GEM");
        gem.SourceUrl.Should().Be("https://example.com");
    }

    [Fact]
    public async Task PostGem_InvalidUrl_ReturnsBadRequest()
    {
        if (TrySkipTest())
        {
            return;
        }

        var response = await Client.PostAsJsonAsync("/api/v1/gems", new
        {
            Url = "htp:/bad-url",
            Title = "Broken"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await ReadValidationProblemAsync(response);

        problem.Errors.Should().ContainKey("Url");
        problem.Errors["Url"].Should().Contain(msg => msg.Contains("absolute URL", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PostGem_EmptyTitle_ReturnsBadRequest()
    {
        if (TrySkipTest())
        {
            return;
        }

        var response = await Client.PostAsJsonAsync("/api/v1/gems", new
        {
            Url = "https://example.org",
            Title = string.Empty
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await ReadValidationProblemAsync(response);

        problem.Errors.Should().ContainKey("Title");
        problem.Errors["Title"].Should().Contain(msg => msg.Contains("must not be empty", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PostGem_CreatesSnapshot_StoresInDatabase()
    {
        if (TrySkipTest())
        {
            return;
        }

        var created = await CreateGemAsync("https://snapshot.example.com", "Snapshot GEM");
        var fetched = await GetGemByIdAsync(created.Id);

        fetched.SnapshotContent.Should().NotBeNullOrWhiteSpace();
        fetched.SnapshotContentType.Should().NotBeNullOrWhiteSpace();
        fetched.SnapshotContent.Should().Be(created.SnapshotContent);
    }

    [Fact]
    public async Task GetGems_ReturnsPagedList()
    {
        if (TrySkipTest())
        {
            return;
        }

        await CreateGemAsync("https://batch.example.com/1", "Gem 1");
        await CreateGemAsync("https://batch.example.com/2", "Gem 2");
        await CreateGemAsync("https://batch.example.com/3", "Gem 3");

        var response = await Client.GetAsync("/api/v1/gems");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await ReadPaginatedGemResponseAsync(response);

        page.Total.Should().Be(3);
        page.Page.Should().Be(1);
        page.PageSize.Should().Be(10);
        page.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetGems_WithPagination_ReturnsCorrectPage()
    {
        if (TrySkipTest())
        {
            return;
        }

        for (var i = 1; i <= 15; i++)
        {
            await CreateGemAsync($"https://paging.example.com/{i}", $"Gem {i}");
        }

        var response = await Client.GetAsync("/api/v1/gems?page=2&pageSize=5");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await ReadPaginatedGemResponseAsync(response);

        page.Page.Should().Be(2);
        page.PageSize.Should().Be(5);
        page.Total.Should().Be(15);
        page.Items.Should().HaveCount(5);

        var expectedTitles = new[] { "Gem 10", "Gem 9", "Gem 8", "Gem 7", "Gem 6" };
        page.Items.Select(item => item.Title).Should().Equal(expectedTitles);
    }

    [Fact(Skip = "Category filtering is not implemented in the current API")] 
    public Task GetGems_WithCategoryFilter_ReturnsFilteredGems() => Task.CompletedTask;

    [Fact]
    public async Task GetGemById_ExistingId_ReturnsGem()
    {
        if (TrySkipTest())
        {
            return;
        }

        var category = await CreateCategoryAsync("Insights", "AI");
        var created = await CreateGemAsync("https://detail.example.com", "Detailed GEM");
        await AssignGemToCategoryAsync(category.Id, created.Id);

        var fetched = await GetGemByIdAsync(created.Id);

        fetched.Id.Should().Be(created.Id);
        fetched.Title.Should().Be("Detailed GEM");
        fetched.SourceUrl.Should().Be("https://detail.example.com");
        fetched.CategoryIds.Should().Contain(category.Id);
    }

    [Fact]
    public async Task GetGemById_NonExistingId_ReturnsNotFound()
    {
        if (TrySkipTest())
        {
            return;
        }

        var response = await Client.GetAsync($"/api/v1/gems/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var message = await ReadMessageAsync(response);
        message.Should().Be("GEM not found");
    }

    [Fact]
    public async Task UpdateGem_ValidInput_UpdatesTitle()
    {
        if (TrySkipTest())
        {
            return;
        }

        var created = await CreateGemAsync("https://update.example.com", "Original Title");
        var updateResponse = await Client.PutAsJsonAsync($"/api/v1/gems/{created.Id}", new
        {
            Title = "Updated Title"
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await DeserializeGemAsync(updateResponse);
        updated.Title.Should().Be("Updated Title");
    }

    [Fact(Skip = "Delete functionality is not implemented yet")]
    public Task DeleteGem_ExistingId_DeletesGem() => Task.CompletedTask;

    private HttpClient Client => _client ?? throw new InvalidOperationException("Http client was not initialized.");

    private async Task<GemDto> CreateGemAsync(string url, string title)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/gems", new { Url = url, Title = title });
        response.EnsureSuccessStatusCode();
        return await DeserializeGemAsync(response);
    }

    private async Task<GemDto> GetGemByIdAsync(Guid id)
    {
        var response = await Client.GetAsync($"/api/v1/gems/{id}");
        response.EnsureSuccessStatusCode();
        return await DeserializeGemAsync(response);
    }

    private static async Task<GemDto> DeserializeGemAsync(HttpResponseMessage response)
    {
        var dto = await response.Content.ReadFromJsonAsync<GemDto>();
        return dto ?? throw new InvalidOperationException("Gem response was empty.");
    }

    private static async Task<PaginatedResponse<GemDto>> ReadPaginatedGemResponseAsync(HttpResponseMessage response)
    {
        var dto = await response.Content.ReadFromJsonAsync<PaginatedResponse<GemDto>>();
        return dto ?? throw new InvalidOperationException("Paginated response was empty.");
    }

    private static async Task<ValidationProblemDetails> ReadValidationProblemAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        return problem ?? throw new InvalidOperationException("Validation response was empty.");
    }

    private async Task<CategoryDto> CreateCategoryAsync(string name, string? description = null)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/categories", new { Name = name, Description = description });
        response.EnsureSuccessStatusCode();
        return await DeserializeCategoryAsync(response);
    }

    private async Task AssignGemToCategoryAsync(Guid categoryId, Guid gemId)
    {
        var response = await Client.PostAsync($"/api/v1/categories/{categoryId}/gems/{gemId}", null);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<CategoryDto> DeserializeCategoryAsync(HttpResponseMessage response)
    {
        var dto = await response.Content.ReadFromJsonAsync<CategoryDto>();
        return dto ?? throw new InvalidOperationException("Category response was empty.");
    }

    private static async Task<string> ReadMessageAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string?>>();
        return payload is not null && payload.TryGetValue("message", out var message) && !string.IsNullOrWhiteSpace(message)
            ? message
            : throw new InvalidOperationException("Expected a message property in the response payload.");
    }

    private bool TrySkipTest()
    {
        if (_skipReason is null)
        {
            return false;
        }

        Console.WriteLine(_skipReason);
        return true;
    }
}
