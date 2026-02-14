using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using InfoDumpManager.Application.Agents.Orchestration;
using InfoDumpManager.Application.Infrastructure.JobQueue;
using InfoDumpManager.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace InfoDumpManager.Tests.Unit.AIAgents;

/// <summary>
/// Unit tests for <see cref="RedisJobQueue{T}"/> verifying Redis Stream operations.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class RedisJobQueueTests
{
    private readonly Mock<IConnectionMultiplexer> _mockRedis;
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly Mock<ILogger<RedisJobQueue<ProcessingJob>>> _mockLogger;
    private readonly RedisJobQueue<ProcessingJob> _queue;

    public RedisJobQueueTests()
    {
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockDatabase = new Mock<IDatabase>();
        _mockLogger = new Mock<ILogger<RedisJobQueue<ProcessingJob>>>();

        _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockDatabase.Object);

        _queue = new RedisJobQueue<ProcessingJob>(_mockRedis.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task EnqueueAsync_ShouldCallStreamAddAsync()
    {
        // Arrange
        var job = CreateTestJob();
        _mockDatabase.Setup(db => db.StreamAddAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<NameValueEntry[]>(),
            It.IsAny<RedisValue?>(),
                It.IsAny<int?>(),
                It.IsAny<bool>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("1-0"));

        // Act
        await _queue.EnqueueAsync(job);

        // Assert
        _mockDatabase.Verify(db => db.StreamAddAsync(
            "jobs:processing",
            It.Is<NameValueEntry[]>(e =>
                e.Length == 1 &&
                e[0].Name == (RedisValue)"payload" &&
                !e[0].Value.IsNullOrEmpty),
            It.IsAny<RedisValue?>(),
            It.IsAny<int?>(),
            It.IsAny<bool>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task EnqueueAsync_ShouldSerializeJobAsJson()
    {
        // Arrange
        var job = CreateTestJob();
        string? capturedJson = null;

        _mockDatabase.Setup(db => db.StreamAddAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<NameValueEntry[]>(),
                It.IsAny<RedisValue?>(),
                It.IsAny<int?>(),
                It.IsAny<bool>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisKey, NameValueEntry[], RedisValue?, int?, bool, CommandFlags>(
                (_, entries, _, _, _, _) => capturedJson = entries[0].Value)
            .ReturnsAsync(new RedisValue("1-0"));

        // Act
        await _queue.EnqueueAsync(job);

        // Assert
        Assert.NotNull(capturedJson);
        var deserialized = JsonSerializer.Deserialize<ProcessingJob>(capturedJson!);
        Assert.NotNull(deserialized);
        Assert.Equal(job.JobId, deserialized!.JobId);
        Assert.Equal(job.GEMId, deserialized.GEMId);
        Assert.Equal(job.ContentText, deserialized.ContentText);
    }

    [Fact]
    public async Task DequeueAsync_WithMessages_ShouldReturnDeserializedJob()
    {
        // Arrange
        var job = CreateTestJob();
        var json = JsonSerializer.Serialize(job);

        SetupConsumerGroupCreation();

        var streamEntry = new StreamEntry(
            "1-0",
            new[] { new NameValueEntry("payload", json) });

        _mockDatabase.Setup(db => db.StreamReadGroupAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<int?>(),
                It.IsAny<bool>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new[] { streamEntry });

        // Act
        var result = await _queue.DequeueAsync(TimeSpan.FromSeconds(1));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(job.JobId, result!.JobId);
        Assert.Equal(job.GEMId, result.GEMId);
        Assert.Equal(job.ContentText, result.ContentText);
    }

    [Fact]
    public async Task DequeueAsync_WithEmptyStream_ShouldReturnNull()
    {
        // Arrange
        SetupConsumerGroupCreation();

        _mockDatabase.Setup(db => db.StreamReadGroupAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<int?>(),
                It.IsAny<bool>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(Array.Empty<StreamEntry>());

        // Act
        var result = await _queue.DequeueAsync(TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DequeueAsync_WithNullEntries_ShouldReturnNull()
    {
        // Arrange
        SetupConsumerGroupCreation();

        _mockDatabase.Setup(db => db.StreamReadGroupAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<int?>(),
                It.IsAny<bool>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync((StreamEntry[]?)null!);

        // Act
        var result = await _queue.DequeueAsync(TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DequeueAsync_WithEmptyPayload_ShouldAcknowledgeAndReturnNull()
    {
        // Arrange
        SetupConsumerGroupCreation();

        var streamEntry = new StreamEntry(
            "1-0",
            new[] { new NameValueEntry("payload", RedisValue.Null) });

        _mockDatabase.Setup(db => db.StreamReadGroupAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<int?>(),
                It.IsAny<bool>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new[] { streamEntry });

        _mockDatabase.Setup(db => db.StreamAcknowledgeAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(1L);

        // Act
        var result = await _queue.DequeueAsync(TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.Null(result);
        _mockDatabase.Verify(db => db.StreamAcknowledgeAsync(
            "jobs:processing", "workers", "1-0", It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task DequeueAsync_ShouldCreateConsumerGroupOnFirstCall()
    {
        // Arrange
        SetupConsumerGroupCreation();

        _mockDatabase.Setup(db => db.StreamReadGroupAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<int?>(),
                It.IsAny<bool>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(Array.Empty<StreamEntry>());

        // Act
        await _queue.DequeueAsync(TimeSpan.FromMilliseconds(50));

        // Assert
        _mockDatabase.Verify(db => db.StreamCreateConsumerGroupAsync(
            "jobs:processing", "workers", "0-0", true, It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task DequeueAsync_ShouldHandleExistingConsumerGroup()
    {
        // Arrange — simulate BUSYGROUP error (consumer group already exists)
        _mockDatabase.Setup(db => db.StreamCreateConsumerGroupAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<bool>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisServerException("BUSYGROUP Consumer Group name already exists"));

        _mockDatabase.Setup(db => db.StreamReadGroupAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<int?>(),
                It.IsAny<bool>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(Array.Empty<StreamEntry>());

        // Act — should not throw
        var result = await _queue.DequeueAsync(TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task MarkCompleteAsync_ShouldLogCompletion()
    {
        // Arrange
        var job = CreateTestJob();

        // Act
        await _queue.MarkCompleteAsync(job);

        // Assert — verifies at least one log call was made
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task MarkFailedAsync_WithRetriesRemaining_ShouldReenqueue()
    {
        // Arrange
        var job = CreateTestJob();
        _mockDatabase.Setup(db => db.StreamAddAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<NameValueEntry[]>(),
                It.IsAny<RedisValue>(),
                It.IsAny<int?>(),
                It.IsAny<bool>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("2-0"));

        // Act
        await _queue.MarkFailedAsync(job, "Test error", 1);

        // Assert — should have re-enqueued
        _mockDatabase.Verify(db => db.StreamAddAsync(
            "jobs:processing",
            It.IsAny<NameValueEntry[]>(),
            It.IsAny<RedisValue?>(),
            It.IsAny<int?>(),
            It.IsAny<bool>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task MarkFailedAsync_WithMaxRetries_ShouldAbandon()
    {
        // Arrange
        var job = CreateTestJob();

        // Act
        await _queue.MarkFailedAsync(job, "Final failure", 3);

        // Assert — should NOT have re-enqueued
        _mockDatabase.Verify(db => db.StreamAddAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<NameValueEntry[]>(),
            It.IsAny<RedisValue?>(),
            It.IsAny<int?>(),
            It.IsAny<bool>(),
            It.IsAny<CommandFlags>()), Times.Never);

        // Should log error
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task MarkFailedAsync_ShouldReenqueueWithIncrementedRetryCount()
    {
        // Arrange
        var job = CreateTestJob();
        string? capturedJson = null;

        _mockDatabase.Setup(db => db.StreamAddAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<NameValueEntry[]>(),
                It.IsAny<RedisValue?>(),
                It.IsAny<int?>(),
                It.IsAny<bool>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisKey, NameValueEntry[], RedisValue?, int?, bool, CommandFlags>(
                (_, entries, _, _, _, _) => capturedJson = entries[0].Value)
            .ReturnsAsync(new RedisValue("2-0"));

        // Act
        await _queue.MarkFailedAsync(job, "Transient error", 1);

        // Assert
        Assert.NotNull(capturedJson);
        var requeued = JsonSerializer.Deserialize<ProcessingJob>(capturedJson!);
        Assert.NotNull(requeued);
        Assert.Equal(2, requeued!.RetryCount);
        Assert.Null(requeued.StartedAt);
    }

    [Fact]
    public async Task DequeueBatchAsync_ShouldReturnMultipleJobs()
    {
        // Arrange
        SetupConsumerGroupCreation();

        var jobs = Enumerable.Range(0, 3).Select(_ => CreateTestJob()).ToList();
        var callCount = 0;

        _mockDatabase.Setup(db => db.StreamReadGroupAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<int?>(),
                It.IsAny<bool>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(() =>
            {
                if (callCount < jobs.Count)
                {
                    var json = JsonSerializer.Serialize(jobs[callCount]);
                    callCount++;
                    return new[] { new StreamEntry($"{callCount}-0", new[] { new NameValueEntry("payload", json) }) };
                }
                return Array.Empty<StreamEntry>();
            });

        // Act
        var result = new List<ProcessingJob>();
        await foreach (var job in _queue.DequeueBatchAsync(3))
        {
            result.Add(job);
        }

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task DequeueBatchAsync_WithFewerJobs_ShouldReturnAvailable()
    {
        // Arrange
        SetupConsumerGroupCreation();

        var job = CreateTestJob();
        var callCount = 0;

        _mockDatabase.Setup(db => db.StreamReadGroupAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<int?>(),
                It.IsAny<bool>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(() =>
            {
                if (callCount == 0)
                {
                    callCount++;
                    var json = JsonSerializer.Serialize(job);
                    return new[] { new StreamEntry("1-0", new[] { new NameValueEntry("payload", json) }) };
                }
                return Array.Empty<StreamEntry>();
            });

        // Act
        var result = new List<ProcessingJob>();
        await foreach (var j in _queue.DequeueBatchAsync(5))
        {
            result.Add(j);
        }

        // Assert
        Assert.Single(result);
        Assert.Equal(job.JobId, result[0].JobId);
    }

    private void SetupConsumerGroupCreation()
    {
        _mockDatabase.Setup(db => db.StreamCreateConsumerGroupAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<bool>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
    }

    private static ProcessingJob CreateTestJob() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test content",
            new ProcessingOptions(), 0, DateTimeOffset.UtcNow, null);
}
