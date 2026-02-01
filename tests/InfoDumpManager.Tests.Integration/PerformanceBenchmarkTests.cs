using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using InfoDumpManager.Infrastructure.Services;
using InfoDumpManager.Tests.Integration.TestUtilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
        elapsedMs.Should().BeLessThan(10000, "Scraping should complete within 10 seconds (NFR-001)");
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
    public async Task HtmlSanitization_LargeContent_CompletesQuickly()
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
    public async Task UrlNormalization_MultipleUrls_CompletesQuickly()
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
    public async Task HtmlSanitization_ScalabilityTest(int paragraphCount)
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
