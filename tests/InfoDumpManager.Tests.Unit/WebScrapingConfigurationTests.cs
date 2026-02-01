using System;
using FluentAssertions;
using InfoDumpManager.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace InfoDumpManager.Tests.Unit;

public sealed class WebScrapingOptionsValidationTests
{
    [Fact]
    public void WebScrapingOptions_WithDefaultValues_HasReasonableDefaults()
    {
        // Arrange
        var options = new WebScrapingOptions();

        // Act & Assert
        options.TimeoutSeconds.Should().Be(10);
        options.RetryCount.Should().Be(3);
        options.RetryBaseDelayMs.Should().Be(250);
        options.CircuitBreakerFailures.Should().Be(5);
        options.CircuitBreakerDurationSeconds.Should().Be(30);
    }

    [Fact]
    public void WebScrapingService_WithZeroTimeout_IsAccepted()
    {
        // Arrange
        var options = Options.Create(new WebScrapingOptions { TimeoutSeconds = 0 });

        // Act - Should not throw during construction
        var service = new WebScrapingService(options, NullLogger<WebScrapingService>.Instance);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void WebScrapingService_WithNegativeTimeout_IsAccepted()
    {
        // Arrange
        var options = Options.Create(new WebScrapingOptions { TimeoutSeconds = -1 });

        // Act - Should not throw during construction
        var service = new WebScrapingService(options, NullLogger<WebScrapingService>.Instance);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void WebScrapingService_WithZeroRetryCount_IsAccepted()
    {
        // Arrange
        var options = Options.Create(new WebScrapingOptions { RetryCount = 0 });

        // Act - Should not throw during construction
        var service = new WebScrapingService(options, NullLogger<WebScrapingService>.Instance);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void WebScrapingService_WithValidCircuitBreakerFailures_IsAccepted()
    {
        // Arrange - Polly requires CircuitBreakerFailures > 0
        var options = Options.Create(new WebScrapingOptions { CircuitBreakerFailures = 3 });

        // Act - Should not throw during construction
        var service = new WebScrapingService(options, NullLogger<WebScrapingService>.Instance);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void WebScrapingService_WithHighCircuitBreakerFailures_IsAccepted()
    {
        // Arrange - Test with high value
        var options = Options.Create(new WebScrapingOptions { CircuitBreakerFailures = 100 });

        // Act
        var service = new WebScrapingService(options, NullLogger<WebScrapingService>.Instance);

        // Assert
        service.Should().NotBeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(60)]
    public void WebScrapingOptions_WithValidTimeoutSeconds_IsAccepted(int timeoutSeconds)
    {
        // Arrange
        var options = Options.Create(new WebScrapingOptions { TimeoutSeconds = timeoutSeconds });

        // Act
        var service = new WebScrapingService(options, NullLogger<WebScrapingService>.Instance);

        // Assert
        service.Should().NotBeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(10)]
    public void WebScrapingOptions_WithValidRetryCount_IsAccepted(int retryCount)
    {
        // Arrange
        var options = Options.Create(new WebScrapingOptions { RetryCount = retryCount });

        // Act
        var service = new WebScrapingService(options, NullLogger<WebScrapingService>.Instance);

        // Assert
        service.Should().NotBeNull();
    }
}

public sealed class MinioOptionsValidationTests
{
    [Fact]
    public void MinioStorageService_WithMissingEndpoint_Throws()
    {
        // Arrange
        var options = Options.Create(new MinioOptions
        {
            Endpoint = null,
            AccessKey = "key",
            SecretKey = "secret",
            BucketName = "bucket",
            UseSsl = false
        });

        // Act
        var act = () => new MinioStorageService(options, NullLogger<MinioStorageService>.Instance);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*endpoint*");
    }

    [Fact]
    public void MinioStorageService_WithEmptyEndpoint_Throws()
    {
        // Arrange
        var options = Options.Create(new MinioOptions
        {
            Endpoint = "",
            AccessKey = "key",
            SecretKey = "secret",
            BucketName = "bucket",
            UseSsl = false
        });

        // Act
        var act = () => new MinioStorageService(options, NullLogger<MinioStorageService>.Instance);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MinioStorageService_WithMissingAccessKey_Throws()
    {
        // Arrange
        var options = Options.Create(new MinioOptions
        {
            Endpoint = "localhost:9000",
            AccessKey = null,
            SecretKey = "secret",
            BucketName = "bucket",
            UseSsl = false
        });

        // Act
        var act = () => new MinioStorageService(options, NullLogger<MinioStorageService>.Instance);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*credentials*");
    }

    [Fact]
    public void MinioStorageService_WithMissingSecretKey_Throws()
    {
        // Arrange
        var options = Options.Create(new MinioOptions
        {
            Endpoint = "localhost:9000",
            AccessKey = "key",
            SecretKey = null,
            BucketName = "bucket",
            UseSsl = false
        });

        // Act
        var act = () => new MinioStorageService(options, NullLogger<MinioStorageService>.Instance);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*credentials*");
    }

    [Fact]
    public void MinioStorageService_WithValidConfiguration_Succeeds()
    {
        // Arrange
        var options = Options.Create(new MinioOptions
        {
            Endpoint = "localhost:9000",
            AccessKey = "minioadmin",
            SecretKey = "minioadmin123",
            BucketName = "gem-snapshots",
            UseSsl = false
        });

        // Act
        var service = new MinioStorageService(options, NullLogger<MinioStorageService>.Instance);

        // Assert
        service.Should().NotBeNull();
    }

    [Theory]
    [InlineData("localhost:9000", false)]
    [InlineData("minio.example.com:9000", true)]
    [InlineData("s3.amazonaws.com", true)]
    public void MinioStorageService_WithVariousEndpoints_Succeeds(string endpoint, bool useSsl)
    {
        // Arrange
        var options = Options.Create(new MinioOptions
        {
            Endpoint = endpoint,
            AccessKey = "key",
            SecretKey = "secret",
            BucketName = "bucket",
            UseSsl = useSsl
        });

        // Act
        var service = new MinioStorageService(options, NullLogger<MinioStorageService>.Instance);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void MinioStorageService_WithNullOptions_Throws()
    {
        // Arrange
        var options = Options.Create<MinioOptions>(null);

        // Act
        var act = () => new MinioStorageService(options, NullLogger<MinioStorageService>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
