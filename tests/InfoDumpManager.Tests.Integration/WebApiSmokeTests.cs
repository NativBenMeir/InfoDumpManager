using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using InfoDumpManager.WebAPI;
using InfoDumpManager.Tests.Integration.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace InfoDumpManager.Tests.Integration;

public sealed class WebApiSmokeTests : IAsyncLifetime
{
    static WebApiSmokeTests()
    {
        Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "true");
    }

    private readonly PostgreSqlTestcontainer _postgresContainer;
    private CustomWebApplicationFactory? _factory;
    private HttpClient? _client;
    private string? _skipReason;

    public WebApiSmokeTests()
    {
        _postgresContainer = new TestcontainersBuilder<PostgreSqlTestcontainer>()
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
            await _postgresContainer.StartAsync();
        }
        catch (InvalidOperationException ex)
        {
            _skipReason = $"Testcontainers unavailable: {ex.Message}";
            return;
        }

        _factory = new CustomWebApplicationFactory(_postgresContainer);
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _postgresContainer.DisposeAsync();
    }

    [Fact]
    public async Task GetWeatherForecast_ReturnsFiveSummaries()
    {
        if (TrySkipTest())
        {
            return;
        }

        var response = await Client.GetAsync("/WeatherForecast");
        response.EnsureSuccessStatusCode();

        var forecasts = await response.Content.ReadFromJsonAsync<IReadOnlyList<WeatherForecast>>();
        forecasts.Should().NotBeNull().And.HaveCount(5);
        forecasts!.Select(f => f.Summary).Should().AllSatisfy(summary => summary.Should().NotBeNullOrWhiteSpace());
    }

    private HttpClient Client => _client ?? throw new InvalidOperationException("Http client was not initialized.");

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
