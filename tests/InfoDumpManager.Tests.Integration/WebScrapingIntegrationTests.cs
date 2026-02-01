using System;
using System.Threading.Tasks;
using FluentAssertions;
using InfoDumpManager.Infrastructure.Services;
using InfoDumpManager.Tests.Integration.TestUtilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Xunit;

namespace InfoDumpManager.Tests.Integration;

[Collection("IntegrationTests")]
public sealed class WebScrapingIntegrationTests
{
    [Fact]
    public async Task WebScrapingService_FetchValidUrl_ReturnsSanitizedHtml()
    {
        await using var server = await MockWebServer.StartAsync(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync("<html><head><title>Test</title></head><body><script>alert('x');</script><div>Safe</div></body></html>");
        });

        var options = Options.Create(new WebScrapingOptions
        {
            TimeoutSeconds = 2,
            RetryCount = 0
        });

        var service = new WebScrapingService(options, NullLogger<WebScrapingService>.Instance);

        var result = await service.ScrapeAsync(server.BaseUrl);

        result.HtmlContent.Should().Contain("Safe");
        result.HtmlContent.Should().NotContain("<script");
        result.Url.Should().StartWith(server.BaseUrl);
    }

    [Fact]
    public async Task WebScrapingService_FetchInvalidUrl_RetriesBeforeFailing()
    {
        await using var server = await MockWebServer.StartAsync(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync("<html><body>Error</body></html>");
        });

        var options = Options.Create(new WebScrapingOptions
        {
            TimeoutSeconds = 2,
            RetryCount = 2,
            RetryBaseDelayMs = 10,
            CircuitBreakerFailures = 5
        });

        var service = new WebScrapingService(options, NullLogger<WebScrapingService>.Instance);

        Func<Task> act = () => service.ScrapeAsync(server.BaseUrl);

        await act.Should().ThrowAsync<InvalidOperationException>();
        server.RequestCount.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task WebScrapingService_Timeouts_OpenCircuitBreakerAfterThreshold()
    {
        await using var server = await MockWebServer.StartAsync(async context =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync("<html><body>Delayed</body></html>");
        });

        var options = Options.Create(new WebScrapingOptions
        {
            TimeoutSeconds = 1,
            RetryCount = 0,
            CircuitBreakerFailures = 5,
            CircuitBreakerDurationSeconds = 30
        });

        var service = new WebScrapingService(options, NullLogger<WebScrapingService>.Instance);

        for (var i = 0; i < options.Value.CircuitBreakerFailures; i++)
        {
            Func<Task> act = () => service.ScrapeAsync(server.BaseUrl);
            await act.Should().ThrowAsync<TimeoutException>();
        }

        Func<Task> breakerAct = () => service.ScrapeAsync(server.BaseUrl);
        await breakerAct.Should().ThrowAsync<BrokenCircuitException>();
    }
}
