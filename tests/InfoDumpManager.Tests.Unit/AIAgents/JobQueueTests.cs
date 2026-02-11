using System.Diagnostics.CodeAnalysis;
using InfoDumpManager.Application.Agents.Orchestration;
using InfoDumpManager.Application.Infrastructure.JobQueue;
using InfoDumpManager.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace InfoDumpManager.Tests.Unit.AIAgents;

[ExcludeFromCodeCoverage]
public sealed class InMemoryJobQueueTests
{
    private readonly Mock<ILogger<InMemoryJobQueue<ProcessingJob>>> _mockLogger;
    private readonly InMemoryJobQueue<ProcessingJob> _queue;

    public InMemoryJobQueueTests()
    {
        _mockLogger = new Mock<ILogger<InMemoryJobQueue<ProcessingJob>>>();
        _queue = new InMemoryJobQueue<ProcessingJob>(_mockLogger.Object);
    }

    [Fact]
    public async Task EnqueueAsync_ShouldAddJobToQueue()
    {
        // Arrange
        var job = CreateTestJob();

        // Act
        await _queue.EnqueueAsync(job);
        var dequeuedJob = await _queue.DequeueAsync(TimeSpan.FromSeconds(1));

        // Assert
        Assert.NotNull(dequeuedJob);
        Assert.Equal(job.JobId, dequeuedJob.JobId);
    }

    [Fact]
    public async Task DequeueAsync_WithEmptyQueue_ShouldReturnNull()
    {
        // Act
        var job = await _queue.DequeueAsync(TimeSpan.FromMilliseconds(100));

        // Assert
        Assert.Null(job);
    }

    [Fact]
    public async Task MarkCompleteAsync_ShouldLogCompletion()
    {
        // Arrange
        var job = CreateTestJob();

        // Act
        await _queue.MarkCompleteAsync(job);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("completed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task MarkFailedAsync_WithLessThan3Retries_ShouldRequeueWithExponentialBackoff()
    {
        // Arrange
        var job = CreateTestJob();
        var retryCount = 1;

        // Act
        await _queue.MarkFailedAsync(job, "Test error", retryCount);

        // Wait for backoff delay (2^1 = 2 seconds) + buffer
        await Task.Delay(TimeSpan.FromMilliseconds(2100));

        var requeuedJob = await _queue.DequeueAsync(TimeSpan.FromSeconds(1));

        // Assert
        Assert.NotNull(requeuedJob);
        Assert.Equal(retryCount + 1, requeuedJob.RetryCount);
    }

    [Fact]
    public async Task MarkFailedAsync_With3Retries_ShouldAbandonJob()
    {
        // Arrange
        var job = CreateTestJob();
        var retryCount = 3;

        // Act
        await _queue.MarkFailedAsync(job, "Test error", retryCount);

        // Wait to ensure job is not requeued
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        var requeuedJob = await _queue.DequeueAsync(TimeSpan.FromMilliseconds(100));

        // Assert
        Assert.Null(requeuedJob);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("abandoned")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData(0, 1)] // 2^0 = 1 second
    [InlineData(1, 2)] // 2^1 = 2 seconds
    [InlineData(2, 4)] // 2^2 = 4 seconds
    public async Task MarkFailedAsync_ShouldUseExponentialBackoff(int retryCount, int expectedDelaySeconds)
    {
        // Arrange
        var job = CreateTestJob();
        var startTime = DateTimeOffset.UtcNow;

        // Act
        await _queue.MarkFailedAsync(job, "Test error", retryCount);

        // Wait for expected delay + buffer
        await Task.Delay(TimeSpan.FromMilliseconds(expectedDelaySeconds * 1000 + 100));

        var requeuedJob = await _queue.DequeueAsync(TimeSpan.FromSeconds(1));
        var endTime = DateTimeOffset.UtcNow;

        // Assert
        Assert.NotNull(requeuedJob);
        var actualDelay = (endTime - startTime).TotalSeconds;
        Assert.True(actualDelay >= expectedDelaySeconds, $"Expected at least {expectedDelaySeconds}s, got {actualDelay}s");
    }

    [Fact]
    public async Task DequeueBatchAsync_ShouldReturnRequestedNumberOfJobs()
    {
        // Arrange
        var batchSize = 3;
        for (var i = 0; i < 5; i++)
        {
            await _queue.EnqueueAsync(CreateTestJob());
        }

        // Act
        var jobs = new List<ProcessingJob>();
        await foreach (var job in _queue.DequeueBatchAsync(batchSize))
        {
            jobs.Add(job);
        }

        // Assert
        Assert.Equal(batchSize, jobs.Count);
    }

    [Fact]
    public async Task DequeueBatchAsync_WithFewerJobsThanBatchSize_ShouldReturnAvailableJobs()
    {
        // Arrange
        var availableJobs = 2;
        var batchSize = 5;
        for (var i = 0; i < availableJobs; i++)
        {
            await _queue.EnqueueAsync(CreateTestJob());
        }

        // Act
        var jobs = new List<ProcessingJob>();
        await foreach (var job in _queue.DequeueBatchAsync(batchSize))
        {
            jobs.Add(job);
        }

        // Assert
        Assert.Equal(availableJobs, jobs.Count);
    }

    private static ProcessingJob CreateTestJob()
    {
        return new ProcessingJob(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test content",
            new ProcessingOptions(),
            0,
            DateTimeOffset.UtcNow,
            null);
    }
}
