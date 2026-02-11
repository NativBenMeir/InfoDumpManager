using System.Diagnostics.CodeAnalysis;
using InfoDumpManager.Application.Agents.Orchestration;
using InfoDumpManager.Application.Infrastructure.JobQueue;
using InfoDumpManager.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace InfoDumpManager.Tests.Integration.AIAgents;

[ExcludeFromCodeCoverage]
public sealed class BackgroundProcessingIntegrationTests
{
    [Fact]
    public async Task BackgroundService_ShouldDrainQueueAndProcessJobs()
    {
        // Arrange
        var mockOrchestrator = new Mock<IContentProcessingOrchestrator>();
        var mockLogger = new Mock<ILogger<ContentProcessingBackgroundService>>();
        var jobQueue = new InMemoryJobQueue<ProcessingJob>(
            Mock.Of<ILogger<InMemoryJobQueue<ProcessingJob>>>());

        mockOrchestrator
            .Setup(x => x.ProcessGEMAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<ProcessingOptions>(),
                It.IsAny<Guid?>()))
            .ReturnsAsync(new ProcessingResult(
                Guid.NewGuid(),
                ProcessingStatus.Completed,
                null,
                null,
                null,
                null,
                null,
                new List<string>(),
                DateTimeOffset.UtcNow));

        var service = new ContentProcessingBackgroundService(
            jobQueue,
            mockOrchestrator.Object,
            mockLogger.Object);

        var job = new ProcessingJob(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test content",
            new ProcessingOptions(),
            0,
            DateTimeOffset.UtcNow,
            null);

        // Act
        await jobQueue.EnqueueAsync(job);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var serviceTask = service.StartAsync(cts.Token);

        // Wait for job to be processed
        await Task.Delay(1000);
        
        await service.StopAsync(CancellationToken.None);

        // Assert
        mockOrchestrator.Verify(
            x => x.ProcessGEMAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<ProcessingOptions>(),
                It.IsAny<Guid?>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task BackgroundService_WithJobFailure_ShouldRetry()
    {
        // Arrange
        var mockOrchestrator = new Mock<IContentProcessingOrchestrator>();
        var mockLogger = new Mock<ILogger<ContentProcessingBackgroundService>>();
        var jobQueue = new InMemoryJobQueue<ProcessingJob>(
            Mock.Of<ILogger<InMemoryJobQueue<ProcessingJob>>>());

        var callCount = 0;
        mockOrchestrator
            .Setup(x => x.ProcessGEMAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<ProcessingOptions>(),
                It.IsAny<Guid?>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return new ProcessingResult(
                    Guid.NewGuid(),
                    callCount < 2 ? ProcessingStatus.Failed : ProcessingStatus.Completed,
                    null,
                    null,
                    null,
                    null,
                    null,
                    callCount < 2 ? new List<string> { "Error" } : new List<string>(),
                    DateTimeOffset.UtcNow);
            });

        var service = new ContentProcessingBackgroundService(
            jobQueue,
            mockOrchestrator.Object,
            mockLogger.Object);

        var job = new ProcessingJob(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test content",
            new ProcessingOptions(),
            0,
            DateTimeOffset.UtcNow,
            null);

        // Act
        await jobQueue.EnqueueAsync(job);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await service.StartAsync(cts.Token);

        // Wait for retries
        await Task.Delay(5000);
        
        await service.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(callCount >= 2, $"Expected at least 2 calls due to retry, got {callCount}");
    }

    [Fact]
    public async Task BackgroundService_WithJobAbandonment_ShouldLogError()
    {
        // Arrange
        var mockOrchestrator = new Mock<IContentProcessingOrchestrator>();
        var mockLogger = new Mock<ILogger<ContentProcessingBackgroundService>>();
        var queueLogger = new Mock<ILogger<InMemoryJobQueue<ProcessingJob>>>();
        var jobQueue = new InMemoryJobQueue<ProcessingJob>(queueLogger.Object);

        mockOrchestrator
            .Setup(x => x.ProcessGEMAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<ProcessingOptions>(),
                It.IsAny<Guid?>()))
            .ReturnsAsync(new ProcessingResult(
                Guid.NewGuid(),
                ProcessingStatus.Failed,
                null,
                null,
                null,
                null,
                null,
                new List<string> { "Persistent error" },
                DateTimeOffset.UtcNow));

        var service = new ContentProcessingBackgroundService(
            jobQueue,
            mockOrchestrator.Object,
            mockLogger.Object);

        var job = new ProcessingJob(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test content",
            new ProcessingOptions(),
            2, // Already retried twice
            DateTimeOffset.UtcNow,
            null);

        // Act
        await jobQueue.EnqueueAsync(job);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await service.StartAsync(cts.Token);

        await Task.Delay(6000); // Wait for all retries + abandonment
        
        await service.StopAsync(CancellationToken.None);

        // Assert
        queueLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("abandoned")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task BackgroundService_ShouldHandleGracefulShutdown()
    {
        // Arrange
        var mockOrchestrator = new Mock<IContentProcessingOrchestrator>();
        var mockLogger = new Mock<ILogger<ContentProcessingBackgroundService>>();
        var jobQueue = new InMemoryJobQueue<ProcessingJob>(
            Mock.Of<ILogger<InMemoryJobQueue<ProcessingJob>>>());

        var service = new ContentProcessingBackgroundService(
            jobQueue,
            mockOrchestrator.Object,
            mockLogger.Object);

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await service.StartAsync(cts.Token);
        
        await Task.Delay(500);
        
        await service.StopAsync(CancellationToken.None);

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("stopped")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
