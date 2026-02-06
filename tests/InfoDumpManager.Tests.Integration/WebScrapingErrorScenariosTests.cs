using System;
using System.Threading.Tasks;
using FluentAssertions;
using InfoDumpManager.Infrastructure.Services;
using InfoDumpManager.Tests.Integration.TestUtilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace InfoDumpManager.Tests.Integration;

/// <summary>
/// Integration tests for error scenarios not covered in basic tests.
/// Tests HTTP error responses, connectivity issues, and timeout behaviors.
/// </summary>
[Collection("IntegrationTests")]
public sealed class WebScrapingErrorScenariosTests
{
    [Fact]
    public async Task WebScrapingService_With404NotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        await using var server = await MockWebServer.StartAsync(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync("Not Found");
        });

        var options = Options.Create(new WebScrapingOptions
        {
            TimeoutSeconds = 2,
            RetryCount = 1,
            RetryBaseDelayMs = 10
        });

        var service = new WebScrapingService(options, NullLogger<WebScrapingService>.Instance);

        // Act
        Func<Task> act = () => service.ScrapeAsync(server.BaseUrl);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*404*");
    }

    [Fact]
    public async Task WebScrapingService_With403Forbidden_ThrowsInvalidOperationException()
    {
        // Arrange
        await using var server = await MockWebServer.StartAsync(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Forbidden");
        });

        var options = Options.Create(new WebScrapingOptions
        {
            TimeoutSeconds = 2,
            RetryCount = 1,
            RetryBaseDelayMs = 10
        });

        var service = new WebScrapingService(options, NullLogger<WebScrapingService>.Instance);

        // Act
        Func<Task> act = () => service.ScrapeAsync(server.BaseUrl);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*403*");
    }

    [Fact]
    public async Task WebScrapingService_With500ServerError_RetriesAndThrows()
    {
        // Arrange
        var requestCount = 0;
        await using var server = await MockWebServer.StartAsync(async context =>
        {
            requestCount++;
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsync("Internal Server Error");
        });

        var options = Options.Create(new WebScrapingOptions
        {
            TimeoutSeconds = 2,
            RetryCount = 2,
            RetryBaseDelayMs = 10
        });

        var service = new WebScrapingService(options, NullLogger<WebScrapingService>.Instance);

        // Act
        Func<Task> act = () => service.ScrapeAsync(server.BaseUrl);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        server.RequestCount.Should().BeGreaterThan(1, "Should have retried");
    }

    [Fact]
    public async Task WebScrapingService_WithEmptyUrl_ThrowsArgumentException()
    {
        // Arrange
        var options = Options.Create(new WebScrapingOptions { TimeoutSeconds = 2 });
        var service = new WebScrapingService(options, NullLogger<WebScrapingService>.Instance);

        // Act
        Func<Task> act = () => service.ScrapeAsync("");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task WebScrapingService_WithNullUrl_ThrowsArgumentException()
    {
        // Arrange
        var options = Options.Create(new WebScrapingOptions { TimeoutSeconds = 2 });
        var service = new WebScrapingService(options, NullLogger<WebScrapingService>.Instance);

        // Act
        Func<Task> act = () => service.ScrapeAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task WebScrapingService_WithWhitespaceUrl_ThrowsArgumentException()
    {
        // Arrange
        var options = Options.Create(new WebScrapingOptions { TimeoutSeconds = 2 });
        var service = new WebScrapingService(options, NullLogger<WebScrapingService>.Instance);

        // Act
        Func<Task> act = () => service.ScrapeAsync("   ");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task WebScrapingService_WithMalformedUrl_ThrowsArgumentException()
    {
        // Arrange
        var options = Options.Create(new WebScrapingOptions { TimeoutSeconds = 2 });
        var service = new WebScrapingService(options, NullLogger<WebScrapingService>.Instance);

        // Act
        Func<Task> act = () => service.ScrapeAsync("not a url");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task WebScrapingService_WithFtpScheme_ThrowsArgumentException()
    {
        // Arrange
        var options = Options.Create(new WebScrapingOptions { TimeoutSeconds = 2 });
        var service = new WebScrapingService(options, NullLogger<WebScrapingService>.Instance);

        // Act
        Func<Task> act = () => service.ScrapeAsync("ftp://example.com");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task WebScrapingService_WithHttpStatusCodesRange_HandlesAppropriately()
    {
        // Arrange
        var testCases = new[] { 500, 502, 503, 504 };

        foreach (var statusCode in testCases)
        {
            await using var server = await MockWebServer.StartAsync(async context =>
            {
                context.Response.StatusCode = statusCode;
                await context.Response.WriteAsync($"Error {statusCode}");
            });

            var options = Options.Create(new WebScrapingOptions
            {
                TimeoutSeconds = 2,
                RetryCount = 0,
                CircuitBreakerFailures = 5
            });

            var service = new WebScrapingService(options, NullLogger<WebScrapingService>.Instance);

            // Act
            Func<Task> act = () => service.ScrapeAsync(server.BaseUrl);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }
    }
}

/// <summary>
/// Integration tests for MinIO storage error scenarios.
/// </summary>
[Collection("IntegrationTests")]
public sealed class MinioStorageErrorScenariosTests
{
    [Fact]
    public async Task MinioStorageService_WithEmptyObjectKey_ThrowsArgumentException()
    {
        // Arrange - using dummy options (won't actually connect)
        var options = Options.Create(new MinioOptions
        {
            Endpoint = "localhost:9000",
            AccessKey = "key",
            SecretKey = "secret",
            BucketName = "bucket",
            UseSsl = false
        });

        var service = new MinioStorageService(options, NullLogger<MinioStorageService>.Instance);

        // Act
        Func<Task> act = () => service.UploadSnapshotAsync("", "<html></html>", "text/html");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task MinioStorageService_WithNullObjectKey_ThrowsArgumentException()
    {
        // Arrange
        var options = Options.Create(new MinioOptions
        {
            Endpoint = "localhost:9000",
            AccessKey = "key",
            SecretKey = "secret",
            BucketName = "bucket",
            UseSsl = false
        });

        var service = new MinioStorageService(options, NullLogger<MinioStorageService>.Instance);

        // Act
        Func<Task> act = () => service.UploadSnapshotAsync(null!, "<html></html>", "text/html");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task MinioStorageService_WithEmptyHtmlContent_ThrowsArgumentException()
    {
        // Arrange
        var options = Options.Create(new MinioOptions
        {
            Endpoint = "localhost:9000",
            AccessKey = "key",
            SecretKey = "secret",
            BucketName = "bucket",
            UseSsl = false
        });

        var service = new MinioStorageService(options, NullLogger<MinioStorageService>.Instance);

        // Act
        Func<Task> act = () => service.UploadSnapshotAsync("key.html", "", "text/html");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task MinioStorageService_WithNullHtmlContent_ThrowsArgumentException()
    {
        // Arrange
        var options = Options.Create(new MinioOptions
        {
            Endpoint = "localhost:9000",
            AccessKey = "key",
            SecretKey = "secret",
            BucketName = "bucket",
            UseSsl = false
        });

        var service = new MinioStorageService(options, NullLogger<MinioStorageService>.Instance);

        // Act
        Func<Task> act = () => service.UploadSnapshotAsync("key.html", null!, "text/html");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task MinioStorageService_WithEmptyContentType_ThrowsArgumentException()
    {
        // Arrange
        var options = Options.Create(new MinioOptions
        {
            Endpoint = "localhost:9000",
            AccessKey = "key",
            SecretKey = "secret",
            BucketName = "bucket",
            UseSsl = false
        });

        var service = new MinioStorageService(options, NullLogger<MinioStorageService>.Instance);

        // Act
        Func<Task> act = () => service.UploadSnapshotAsync("key.html", "<html></html>", "");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task MinioStorageService_GetSnapshot_WithEmptyKey_ThrowsArgumentException()
    {
        // Arrange
        var options = Options.Create(new MinioOptions
        {
            Endpoint = "localhost:9000",
            AccessKey = "key",
            SecretKey = "secret",
            BucketName = "bucket",
            UseSsl = false
        });

        var service = new MinioStorageService(options, NullLogger<MinioStorageService>.Instance);

        // Act
        Func<Task> act = () => service.GetSnapshotAsync("");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task MinioStorageService_GetSnapshot_WithNullKey_ThrowsArgumentException()
    {
        // Arrange
        var options = Options.Create(new MinioOptions
        {
            Endpoint = "localhost:9000",
            AccessKey = "key",
            SecretKey = "secret",
            BucketName = "bucket",
            UseSsl = false
        });

        var service = new MinioStorageService(options, NullLogger<MinioStorageService>.Instance);

        // Act
        Func<Task> act = () => service.GetSnapshotAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
