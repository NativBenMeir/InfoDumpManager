using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using InfoDumpManager.Application.Categories.DTOs;
using InfoDumpManager.Application.GEMs.DTOs;
using InfoDumpManager.Tests.Integration.Fixtures;
using InfoDumpManager.WebAPI;
using InfoDumpManager.WebAPI.Contracts.Auth;
using InfoDumpManager.WebAPI.Contracts.Categories;
using InfoDumpManager.WebAPI.Contracts.GEMs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace InfoDumpManager.Tests.Integration;

[Collection("IntegrationTests")]
public sealed class ApiIntegrationTests : IAsyncLifetime
{
    private readonly PostgresTestcontainerFixture _fixture;
    private readonly InfoDumpWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ApiIntegrationTests(PostgresTestcontainerFixture fixture)
    {
        _fixture = fixture;
        Environment.SetEnvironmentVariable("JWT_SECRET", "TestJwtSecretKey1234567890123456");
        _factory = new InfoDumpWebApplicationFactory(_fixture);
        _client = _factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task TEST_021_RegisterUser_ReturnsToken()
    {
        var request = BuildRegisterRequest();
        var response = await PostJsonAsync("/api/v1/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();
        payload.Should().NotBeNull();
        payload!.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TEST_022_LoginWithValidCredentials_ReturnsToken()
    {
        var userName = GenerateUserName();
        var password = "SuperSecret123!";
        var tenantId = Guid.NewGuid();
        await RegisterUserAsync(userName, password, tenantId);

        var loginResponse = await PostJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            UserName = userName,
            Password = password
        });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        payload.Should().NotBeNull();
        payload!.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TEST_023_LoginWithInvalidCredentials_ReturnsUnauthorized()
    {
        var userName = GenerateUserName();
        var password = "SuperSecret123!";
        var tenantId = Guid.NewGuid();
        await RegisterUserAsync(userName, password, tenantId);

        var response = await PostJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            UserName = userName,
            Password = "WrongPassword"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TEST_024_CreateGemWithToken_ReturnsCreated()
    {
        var auth = await RegisterAndAuthenticateAsync();
        var createRequest = BuildCreateGemRequest();

        var response = await PostJsonAsync("/api/v1/gems", createRequest, auth.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var gem = await response.Content.ReadFromJsonAsync<GEMDto>();
        gem.Should().NotBeNull();
        gem!.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task TEST_025_CreateGemWithoutToken_ReturnsUnauthorized()
    {
        var createRequest = BuildCreateGemRequest();

        var response = await PostJsonAsync("/api/v1/gems", createRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TEST_026_GetGemById_ReturnsGem()
    {
        var auth = await RegisterAndAuthenticateAsync();
        var createRequest = BuildCreateGemRequest();
        var createResponse = await PostJsonAsync("/api/v1/gems", createRequest, auth.AccessToken);
        var createdGem = await createResponse.Content.ReadFromJsonAsync<GEMDto>();

        createdGem.Should().NotBeNull();
        var gemId = createdGem!.Id;

        var getResponse = await GetAsync($"/api/v1/gems/{gemId}", auth.AccessToken);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var loaded = await getResponse.Content.ReadFromJsonAsync<GEMDto>();
        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(gemId);
    }

    [Fact]
    public async Task TEST_027_CreateCategory_ReturnsCreated()
    {
        var auth = await RegisterAndAuthenticateAsync();
        var response = await PostJsonAsync("/api/v1/categories", new CreateCategoryRequest
        {
            Name = "Integration Category",
            Description = "Created during integration tests"
        }, auth.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var category = await response.Content.ReadFromJsonAsync<CategoryDto>();
        category.Should().NotBeNull();
        category!.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task TEST_029_CategoryDuplicateName_ReturnsStructuredProblem()
    {
        var auth = await RegisterAndAuthenticateAsync();
        var request = new CreateCategoryRequest
        {
            Name = "Duplicate Category",
            Description = "First attempt"
        };

        var first = await PostJsonAsync("/api/v1/categories", request, auth.AccessToken);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await PostJsonAsync("/api/v1/categories", request, auth.AccessToken);
        second.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        second.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await second.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("An unexpected error occurred.");
    }

    [Fact]
    public async Task TEST_030_ListGems_ReturnsPaginatedCollection()
    {
        var auth = await RegisterAndAuthenticateAsync();

        for (var i = 0; i < 3; i++)
        {
            var request = BuildCreateGemRequest();
            request.Title = $"Integration Gem {i}";
            request.Url = $"https://example.com/content/{i}";
            request.SourceUrl = $"https://source.example.com/{i}";
            var createResponse = await PostJsonAsync("/api/v1/gems", request, auth.AccessToken);
            createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var response = await GetAsync("/api/v1/gems?pageNumber=1&pageSize=2", auth.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<GemListResponse>();
        payload.Should().NotBeNull();
        payload!.Items.Should().HaveCount(2);
        payload.Total.Should().BeGreaterThanOrEqualTo(3);
        payload.PageNumber.Should().Be(1);
        payload.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task TEST_031_AssignCategoryToGem_ReturnsNoContent()
    {
        var auth = await RegisterAndAuthenticateAsync();
        var createGemRequest = BuildCreateGemRequest();
        var gemResponse = await PostJsonAsync("/api/v1/gems", createGemRequest, auth.AccessToken);
        var gem = await gemResponse.Content.ReadFromJsonAsync<GEMDto>();

        var categoryResponse = await PostJsonAsync("/api/v1/categories", new CreateCategoryRequest
        {
            Name = "Assignable Category",
            Description = "Category for assignment"
        }, auth.AccessToken);
        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryDto>();

        var assignResponse = await PutJsonAsync($"/api/v1/gems/{gem!.Id}/category", new AssignCategoryRequest
        {
            CategoryId = category!.Id
        }, auth.AccessToken);

        assignResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task TEST_032_ListCategories_ReturnsCreatedEntries()
    {
        var auth = await RegisterAndAuthenticateAsync();

        await PostJsonAsync("/api/v1/categories", new CreateCategoryRequest
        {
            Name = "List Category A",
            Description = "First"
        }, auth.AccessToken);

        await PostJsonAsync("/api/v1/categories", new CreateCategoryRequest
        {
            Name = "List Category B",
            Description = "Second"
        }, auth.AccessToken);

        var response = await GetAsync("/api/v1/categories", auth.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var categories = await response.Content.ReadFromJsonAsync<List<CategoryDto>>();
        categories.Should().NotBeNull();
        categories!.Should().Contain(c => c.Name == "List Category A");
        categories.Should().Contain(c => c.Name == "List Category B");
    }

    [Fact]
    public async Task TEST_033_UpdateCategory_ReturnsNoContent()
    {
        var auth = await RegisterAndAuthenticateAsync();
        var response = await PostJsonAsync("/api/v1/categories", new CreateCategoryRequest
        {
            Name = "Original Name",
            Description = "Original"
        }, auth.AccessToken);
        var category = await response.Content.ReadFromJsonAsync<CategoryDto>();

        var updateResponse = await PutJsonAsync($"/api/v1/categories/{category!.Id}", new UpdateCategoryRequest
        {
            Name = "Updated Name",
            Description = "Updated"
        }, auth.AccessToken);

        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await GetAsync("/api/v1/categories", auth.AccessToken);
        var categories = await listResponse.Content.ReadFromJsonAsync<List<CategoryDto>>();
        categories.Should().Contain(c => c.Id == category.Id && c.Name == "Updated Name" && c.Description == "Updated");
    }

    [Fact]
    public async Task TEST_034_DeleteCategory_ReturnsNoContent()
    {
        var auth = await RegisterAndAuthenticateAsync();
        var response = await PostJsonAsync("/api/v1/categories", new CreateCategoryRequest
        {
            Name = "Delete Category",
            Description = "To be deleted"
        }, auth.AccessToken);
        var category = await response.Content.ReadFromJsonAsync<CategoryDto>();

        var deleteResponse = await DeleteAsync($"/api/v1/categories/{category!.Id}", auth.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await GetAsync("/api/v1/categories", auth.AccessToken);
        var categories = await listResponse.Content.ReadFromJsonAsync<List<CategoryDto>>();
        categories.Should().NotContain(c => c.Id == category.Id);
    }

    [Fact]
    public async Task TEST_035_InvalidToken_ReturnsUnauthorized()
    {
        var response = await GetAsync("/api/v1/gems", "invalid-token");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string GenerateUserName() => $"user_{Guid.NewGuid():N}";

    private static RegisterRequest BuildRegisterRequest()
    {
        var userName = GenerateUserName();
        return new RegisterRequest
        {
            TenantId = Guid.NewGuid(),
            UserName = userName,
            Email = $"{userName}@example.com",
            DisplayName = "Integration Tester",
            Password = "SuperSecret123!"
        };
    }

    private static CreateGemRequest BuildCreateGemRequest()
    {
        return new CreateGemRequest
        {
            Title = "Integration Gem",
            Url = "https://example.com/content",
            SourceUrl = "https://source.example.com",
            SnapshotHtml = "<html><body>Integration snapshot</body></html>",
            SnapshotMimeType = "text/html",
            SnapshotCapturedAt = DateTimeOffset.UtcNow
        };
    }

    private async Task<AuthResponse> RegisterAndAuthenticateAsync()
    {
        var request = BuildRegisterRequest();
        var response = await PostJsonAsync("/api/v1/auth/register", request);
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!;
    }

    private async Task RegisterUserAsync(string userName, string password, Guid tenantId)
    {
        var response = await PostJsonAsync("/api/v1/auth/register", new RegisterRequest
        {
            TenantId = tenantId,
            UserName = userName,
            Email = $"{userName}@example.com",
            DisplayName = "Integration Tester",
            Password = password
        });

        response.EnsureSuccessStatusCode();
    }

    private Task<HttpResponseMessage> PostJsonAsync<T>(string uri, T payload, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(payload)
        };

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PutJsonAsync<T>(string uri, T payload, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, uri)
        {
            Content = JsonContent.Create(payload)
        };

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> DeleteAsync(string uri, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, uri);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> GetAsync(string uri, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return _client.SendAsync(request);
    }

    private sealed record GemListResponse(IReadOnlyList<GEMDto> Items, int PageNumber, int PageSize, int Total);

    private sealed class InfoDumpWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly PostgresTestcontainerFixture _fixture;

        public InfoDumpWebApplicationFactory(PostgresTestcontainerFixture fixture)
        {
            _fixture = fixture;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                var overrides = new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = _fixture.ConnectionString,
                    ["JwtSettings:Issuer"] = "InfoDumpManager",
                    ["JwtSettings:Audience"] = "InfoDumpManagerAPI",
                    ["JwtSettings:Secret"] = "TestJwtSecretKey1234567890123456",
                    ["JwtSettings:ExpiresMinutes"] = "60"
                };

                config.AddInMemoryCollection(overrides!);
            });
        }
    }
}
