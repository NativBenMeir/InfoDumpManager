using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using InfoDumpManager.Application.Agents;
using MediatR;
using InfoDumpManager.Application.Agents.Orchestration;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Infrastructure.Services;
using InfoDumpManager.Tests.Integration.TestUtilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace InfoDumpManager.Tests.Integration;

/// <summary>
/// Performance benchmark tests to validate that system meets non-functional requirements.
/// These tests measure performance characteristics like ingestion time and resource usage.
/// </summary>
[Collection("IntegrationTests")]
public sealed class PerformanceBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public PerformanceBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task WebScrapingService_ValidUrl_CompletesWithinTimeout()
    {
        // Arrange - Simulate a typical web page (10KB HTML)
        var htmlContent = @"
<!DOCTYPE html>
<html>
<head>
    <title>Performance Test Page</title>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
</head>
<body>
    <h1>Performance Test</h1>
    <p>This is a test page for performance benchmarking.</p>
    " + GenerateLargeContent(1000) + @"
</body>
</html>";

        await using var server = await MockWebServer.StartAsync(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync(htmlContent);
        });

        var options = Options.Create(new WebScrapingOptions
        {
            TimeoutSeconds = 10,
            RetryCount = 0
        });

        var service = new WebScrapingService(options, NullLogger<WebScrapingService>.Instance);
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await service.ScrapeAsync(server.BaseUrl);

        stopwatch.Stop();

        // Assert - Should complete within reasonable time
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        _output.WriteLine($"Web scraping completed in {elapsedMs}ms");

        result.Should().NotBeNull();
        result.HtmlContent.Should().NotBeNullOrEmpty();
        var maxMs = PerformanceTestSettings.GetLong("IDM_PERF_SCRAPE_MAX_MS", 15000);
        elapsedMs.Should().BeLessThan(maxMs, $"Scraping should complete within {maxMs}ms (NFR-001)");
    }

    [Fact]
    public async Task WebScrapingService_MultipleRequests_AverageCompletionTime()
    {
        // Arrange
        var htmlContent = @"
<!DOCTYPE html>
<html>
<head><title>Test</title></head>
<body><p>Content</p></body>
</html>";

        await using var server = await MockWebServer.StartAsync(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync(htmlContent);
        });

        var options = Options.Create(new WebScrapingOptions
        {
            TimeoutSeconds = 10,
            RetryCount = 0
        });

        var service = new WebScrapingService(options, NullLogger<WebScrapingService>.Instance);
        var totalTime = 0L;
        var iterations = 3;

        // Act
        for (int i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            await service.ScrapeAsync(server.BaseUrl);
            stopwatch.Stop();
            totalTime += stopwatch.ElapsedMilliseconds;
        }

        var averageTime = totalTime / iterations;
        var maxAverageMs = PerformanceTestSettings.GetLong("IDM_PERF_AVG_MS", 10000);

        // Assert
        _output.WriteLine($"Average scraping time over {iterations} iterations: {averageTime}ms");
        averageTime.Should().BeLessThan(maxAverageMs, $"Average should be within {maxAverageMs}ms");
    }

    [Fact]
    public void HtmlSanitization_LargeContent_CompletesQuickly()
    {
        // Arrange - Create large HTML with many script tags
        var largeHtml = "<div>" + string.Join("", Enumerable.Range(1, 100).Select(i =>
            $"<p>Paragraph {i}</p><script>alert({i})</script>")) + "</div>";

        var stopwatch = Stopwatch.StartNew();

        // Act
        var sanitized = WebScrapingUtilities.SanitizeHtml(largeHtml);

        stopwatch.Stop();

        // Assert
        _output.WriteLine($"HTML sanitization completed in {stopwatch.ElapsedMilliseconds}ms for large content");
        
        sanitized.Should().NotContain("<script");
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "Sanitization should be fast");
    }

    [Fact]
    public void UrlNormalization_MultipleUrls_CompletesQuickly()
    {
        // Arrange
        var urls = new[]
        {
            "https://example.com",
            "http://example.com/path?query=1",
            "https://subdomain.example.com/page#anchor",
            "https://example.com:8080/api",
            "https://example.com/path/to/resource"
        };

        var stopwatch = Stopwatch.StartNew();

        // Act
        foreach (var url in urls)
        {
            WebScrapingUtilities.NormalizeUrl(url);
        }

        stopwatch.Stop();

        // Assert
        _output.WriteLine($"URL normalization for {urls.Length} URLs completed in {stopwatch.ElapsedMilliseconds}ms");
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, "URL normalization should be very fast");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task WebScrapingService_Throughput_MeasurementTest()
    {
        // Arrange - Measure throughput (requests per second)
        await using var server = await MockWebServer.StartAsync(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync("<html><body>Test</body></html>");
        });

        var options = Options.Create(new WebScrapingOptions
        {
            TimeoutSeconds = 2,
            RetryCount = 0
        });

        var service = new WebScrapingService(options, NullLogger<WebScrapingService>.Instance);
        var stopwatch = Stopwatch.StartNew();
        var count = 0;

        // Act - Run requests for 5 seconds and measure throughput
        while (stopwatch.ElapsedMilliseconds < 5000)
        {
            await service.ScrapeAsync(server.BaseUrl);
            count++;
        }

        stopwatch.Stop();

        // Assert
        var throughput = (double)count / (stopwatch.ElapsedMilliseconds / 1000.0);
        _output.WriteLine($"Web scraping throughput: {throughput:F2} requests/second over {stopwatch.ElapsedMilliseconds}ms");
        var minThroughput = PerformanceTestSettings.GetDouble("IDM_PERF_MIN_THROUGHPUT", 0.05);
        
        // Very rough threshold - actual production requirements may vary
        throughput.Should().BeGreaterThan(minThroughput, $"Should handle at least {minThroughput} requests per second");
    }

    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    public void HtmlSanitization_ScalabilityTest(int paragraphCount)
    {
        // Arrange - Create HTML with N paragraphs and scripts
        var html = "<html><body>" +
            string.Join("", Enumerable.Range(1, paragraphCount).Select(i =>
                $"<p>Paragraph {i}</p><script>var x = {i};</script>")) +
            "</body></html>";

        var stopwatch = Stopwatch.StartNew();

        // Act
        var sanitized = WebScrapingUtilities.SanitizeHtml(html);

        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Sanitized {paragraphCount} paragraphs in {stopwatch.ElapsedMilliseconds}ms");
        sanitized.Should().NotContain("<script");
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, $"Should handle {paragraphCount} paragraphs quickly");
    }

    [Fact]
    public async Task BatchProcessing_WithConcurrencyLimit_CompletesWithinExpectedTime()
    {
        // Arrange
        var agents = new List<IAgent>
        {
            new DelayAgent(AgentCapability.Summarization, "Summarization", TimeSpan.FromMilliseconds(50), includeSummary: true),
            new DelayAgent(AgentCapability.Categorization, "Categorization", TimeSpan.FromMilliseconds(50)),
            new DelayAgent(AgentCapability.Tagging, "Tagging", TimeSpan.FromMilliseconds(50)),
            new DelayAgent(AgentCapability.Validation, "Validation", TimeSpan.FromMilliseconds(50))
        };

        var orchestrator = CreateOrchestrator(agents);
        var items = Enumerable.Range(0, 12)
            .Select(_ => (Guid.NewGuid(), Guid.NewGuid(), "Batch content"))
            .ToList();

        var options = new ProcessingOptions(RunValidation: false, MaxConcurrentJobs: 3);
        var maxMs = PerformanceTestSettings.GetLong("IDM_PERF_AI_BATCH_MS", 5000);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await orchestrator.ProcessBatchAsync(items, options);
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"AI batch processed in {stopwatch.ElapsedMilliseconds}ms for {items.Count} items");
        result.Status.Should().Be(ProcessingStatus.Failed);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxMs, $"Batch processing should complete within {maxMs}ms");
    }

    private static string GenerateLargeContent(int paragraphs)
    {
        var content = "";
        for (int i = 0; i < paragraphs; i++)
        {
            content += $@"
    <p>Paragraph {i + 1}: This is test content for performance benchmarking. 
       Lorem ipsum dolor sit amet, consectetur adipiscing elit. 
       Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.</p>";
        }
        return content;
    }

        private static ContentProcessingOrchestrator CreateOrchestrator(IReadOnlyCollection<IAgent> agents)
        {
            var unitOfWork = new Mock<IUnitOfWork>();
            var gemRepository = new Mock<IGEMRepository>();

            unitOfWork.SetupGet(x => x.GEMs).Returns(gemRepository.Object);
            unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);

            var services = new ServiceCollection();
            services.AddScoped<IUnitOfWork>(_ => unitOfWork.Object);
            services.AddScoped<IMediator>(_ => Mock.Of<IMediator>());
            foreach (var agent in agents)
            {
                services.AddScoped<IAgent>(_ => agent);
            }

            var provider = services.BuildServiceProvider();
            var jobTracker = new InMemoryJobTracker();
            return new ContentProcessingOrchestrator(
                provider.GetRequiredService<IServiceScopeFactory>(),
                jobTracker,
                NullLogger<ContentProcessingOrchestrator>.Instance);
        }

        private sealed class DelayAgent : IAgent
        {
            private readonly TimeSpan _delay;
            private readonly bool _includeSummary;

            public DelayAgent(AgentCapability capability, string name, TimeSpan delay, bool includeSummary = false)
            {
                Capability = capability;
                Name = name;
                _delay = delay;
                _includeSummary = includeSummary;
            }

            public string Name { get; }

            public AgentCapability Capability { get; }

            public async Task<AgentResult> ExecuteAsync(AgentContext context)
            {
                await Task.Delay(_delay);

                var payload = new Dictionary<string, object>();
                if (_includeSummary)
                {
                    payload["summary"] = "Batch summary";
                    payload["model"] = "perf-model";
                    payload["tokenCount"] = 10;
                }

                return new AgentResult(
                    true,
                    "ok",
                    new AgentResultData(Name, DateTimeOffset.UtcNow, payload),
                    new AgentMetrics(10, 0.001m, _delay, 0, "perf"));
            }
        }
}

/// <summary>
/// Load test simulating multiple concurrent web scraping requests.
/// Validates that the system can handle concurrent load.
/// </summary>
[Collection("IntegrationTests")]
public sealed class LoadTestingTests
{
    private readonly ITestOutputHelper _output;

    public LoadTestingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait("Category", "Load")]
    public async Task WebScrapingService_MultipleSimultaneousRequests_AllSucceed()
    {
        // Arrange - Create mock server
        await using var server = await MockWebServer.StartAsync(async context =>
        {
            await Task.Delay(50); // Simulate some processing time
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync("<html><body>Load Test</body></html>");
        });

        var options = Options.Create(new WebScrapingOptions
        {
            TimeoutSeconds = 10,
            RetryCount = 1
        });

        var service = new WebScrapingService(options, NullLogger<WebScrapingService>.Instance);
        var stopwatch = Stopwatch.StartNew();

        // Act - Send 10 concurrent requests
        var tasks = Enumerable.Range(1, 10)
            .Select(i => service.ScrapeAsync(server.BaseUrl))
            .ToList();

        var results = await Task.WhenAll(tasks);

        stopwatch.Stop();

        // Assert
        _output.WriteLine($"10 concurrent requests completed in {stopwatch.ElapsedMilliseconds}ms");
        var maxConcurrentMs = PerformanceTestSettings.GetLong("IDM_PERF_CONCURRENT_MAX_MS", 70000);

        results.Should().HaveCount(10);
        results.Should().AllSatisfy(r => r.Should().NotBeNull());
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxConcurrentMs, $"Concurrent requests should complete within {maxConcurrentMs}ms");
    }
}

internal static class PerformanceTestSettings
{
    public static long GetLong(string name, long defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return long.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    public static double GetDouble(string name, double defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return double.TryParse(value, out var parsed) ? parsed : defaultValue;
    }
}
