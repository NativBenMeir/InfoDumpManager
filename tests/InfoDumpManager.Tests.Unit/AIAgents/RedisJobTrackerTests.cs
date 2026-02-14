using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using InfoDumpManager.Application.Agents.Orchestration;
using InfoDumpManager.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace InfoDumpManager.Tests.Unit.AIAgents;

/// <summary>
/// Unit tests for <see cref="RedisJobTracker"/> verifying Redis-backed job status tracking.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class RedisJobTrackerTests
{
    private readonly Mock<IConnectionMultiplexer> _mockRedis;
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly Mock<ISubscriber> _mockSubscriber;
    private readonly Mock<ILogger<RedisJobTracker>> _mockLogger;
    private readonly RedisJobTracker _tracker;

    public RedisJobTrackerTests()
    {
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockDatabase = new Mock<IDatabase>();
        _mockSubscriber = new Mock<ISubscriber>();
        _mockLogger = new Mock<ILogger<RedisJobTracker>>();

        _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockDatabase.Object);
        _mockRedis.Setup(r => r.GetSubscriber(It.IsAny<object>()))
            .Returns(_mockSubscriber.Object);

        _tracker = new RedisJobTracker(_mockRedis.Object, _mockLogger.Object);
    }

    [Fact]
    public void UpdateStatus_ShouldStoreStatusInRedis()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var expectedKey = $"job:status:{jobId}";

        // Act
        _tracker.UpdateStatus(jobId, ProcessingStatus.Processing, 50, "Half done");

        // Assert
        _mockDatabase.Verify(db => db.StringSet(
            (RedisKey)expectedKey,
            It.Is<RedisValue>(v => !v.IsNullOrEmpty),
            TimeSpan.FromHours(24),
            false,
            When.Always,
            CommandFlags.FireAndForget), Times.Once);
    }

    [Fact]
    public void UpdateStatus_ShouldPublishUpdateViaPubSub()
    {
        // Arrange
        var jobId = Guid.NewGuid();

        // Act
        _tracker.UpdateStatus(jobId, ProcessingStatus.Completed, 100, "Done");

        // Assert
        _mockSubscriber.Verify(sub => sub.Publish(
            It.Is<RedisChannel>(ch => ch.ToString() == $"job:updates:{jobId}"),
            It.Is<RedisValue>(v => !v.IsNullOrEmpty),
            CommandFlags.FireAndForget), Times.Once);
    }

    [Fact]
    public void UpdateStatus_ShouldSerializeCorrectStatusData()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        string? capturedJson = null;

        _mockDatabase.Setup(db => db.StringSet(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, bool, When, CommandFlags>(
                (_, value, _, _, _, _) => capturedJson = value);

        // Act
        _tracker.UpdateStatus(jobId, ProcessingStatus.Processing, 75, "Almost there");

        // Assert
        Assert.NotNull(capturedJson);
        var status = JsonSerializer.Deserialize<JobStatus>(capturedJson!);
        Assert.NotNull(status);
        Assert.Equal(jobId, status!.JobId);
        Assert.Equal(ProcessingStatus.Processing, status.Status);
        Assert.Equal(75, status.ProgressPercent);
        Assert.Equal("Almost there", status.Message);
    }

    [Fact]
    public void UpdateStatus_ShouldPublishSerializedUpdateData()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        string? capturedJson = null;

        _mockSubscriber.Setup(sub => sub.Publish(
                It.IsAny<RedisChannel>(),
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisChannel, RedisValue, CommandFlags>(
                (_, value, _) => capturedJson = value);

        // Act
        _tracker.UpdateStatus(jobId, ProcessingStatus.Failed, 30, "Error occurred");

        // Assert
        Assert.NotNull(capturedJson);
        var update = JsonSerializer.Deserialize<JobStatusUpdate>(capturedJson!);
        Assert.NotNull(update);
        Assert.Equal(jobId, update!.JobId);
        Assert.Equal(ProcessingStatus.Failed, update.Status);
        Assert.Equal(30, update.ProgressPercent);
        Assert.Equal("Error occurred", update.Message);
    }

    [Fact]
    public async Task GetJobStatusAsync_WithExistingStatus_ShouldReturnDeserializedStatus()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var storedStatus = new JobStatus(jobId, ProcessingStatus.Completed, 100, "Done", DateTimeOffset.UtcNow);
        var json = JsonSerializer.Serialize(storedStatus);

        _mockDatabase.Setup(db => db.StringGetAsync(
                (RedisKey)$"job:status:{jobId}",
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue(json));

        // Act
        var result = await _tracker.GetJobStatusAsync(jobId);

        // Assert
        Assert.Equal(jobId, result.JobId);
        Assert.Equal(ProcessingStatus.Completed, result.Status);
        Assert.Equal(100, result.ProgressPercent);
        Assert.Equal("Done", result.Message);
    }

    [Fact]
    public async Task GetJobStatusAsync_WithNoStatus_ShouldReturnPendingDefault()
    {
        // Arrange
        var jobId = Guid.NewGuid();

        _mockDatabase.Setup(db => db.StringGetAsync(
                (RedisKey)$"job:status:{jobId}",
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        // Act
        var result = await _tracker.GetJobStatusAsync(jobId);

        // Assert
        Assert.Equal(jobId, result.JobId);
        Assert.Equal(ProcessingStatus.Pending, result.Status);
        Assert.Equal(0, result.ProgressPercent);
        Assert.Equal("Pending", result.Message);
    }

    [Fact]
    public async Task GetJobStatusAsync_WithEmptyString_ShouldReturnPendingDefault()
    {
        // Arrange
        var jobId = Guid.NewGuid();

        _mockDatabase.Setup(db => db.StringGetAsync(
                (RedisKey)$"job:status:{jobId}",
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.EmptyString);

        // Act
        var result = await _tracker.GetJobStatusAsync(jobId);

        // Assert
        Assert.Equal(ProcessingStatus.Pending, result.Status);
    }

    [Fact]
    public async Task GetJobStatusAsync_ShouldQueryCorrectRedisKey()
    {
        // Arrange
        var jobId = Guid.NewGuid();

        _mockDatabase.Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        // Act
        await _tracker.GetJobStatusAsync(jobId);

        // Assert
        _mockDatabase.Verify(db => db.StringGetAsync(
            (RedisKey)$"job:status:{jobId}",
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public void UpdateStatus_ShouldSetTtlOf24Hours()
    {
        // Arrange
        var jobId = Guid.NewGuid();

        // Act
        _tracker.UpdateStatus(jobId, ProcessingStatus.Pending, 0, "Queued");

        // Assert
        _mockDatabase.Verify(db => db.StringSet(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            TimeSpan.FromHours(24),
            It.IsAny<bool>(),
            It.IsAny<When>(),
            CommandFlags.FireAndForget), Times.Once);
    }

    [Fact]
    public void UpdateStatus_ShouldUseFireAndForgetForPerformance()
    {
        // Arrange
        var jobId = Guid.NewGuid();

        // Act
        _tracker.UpdateStatus(jobId, ProcessingStatus.Processing, 10, "Starting");

        // Assert — both StringSet and Publish should use FireAndForget
        _mockDatabase.Verify(db => db.StringSet(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<bool>(),
            It.IsAny<When>(),
            CommandFlags.FireAndForget), Times.Once);

        _mockSubscriber.Verify(sub => sub.Publish(
            It.IsAny<RedisChannel>(),
            It.IsAny<RedisValue>(),
            CommandFlags.FireAndForget), Times.Once);
    }

    [Theory]
    [InlineData(ProcessingStatus.Pending)]
    [InlineData(ProcessingStatus.Processing)]
    [InlineData(ProcessingStatus.Completed)]
    [InlineData(ProcessingStatus.Failed)]
    [InlineData(ProcessingStatus.Cancelled)]
    public void UpdateStatus_ShouldHandleAllProcessingStatuses(ProcessingStatus status)
    {
        // Arrange
        var jobId = Guid.NewGuid();

        // Act & Assert — should not throw for any status
        _tracker.UpdateStatus(jobId, status, 50, $"Status: {status}");

        _mockDatabase.Verify(db => db.StringSet(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<bool>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Theory]
    [InlineData(ProcessingStatus.Pending)]
    [InlineData(ProcessingStatus.Processing)]
    [InlineData(ProcessingStatus.Completed)]
    [InlineData(ProcessingStatus.Failed)]
    [InlineData(ProcessingStatus.Cancelled)]
    public async Task GetJobStatusAsync_ShouldDeserializeAllStatuses(ProcessingStatus status)
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var storedStatus = new JobStatus(jobId, status, 50, "Test", DateTimeOffset.UtcNow);
        var json = JsonSerializer.Serialize(storedStatus);

        _mockDatabase.Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue(json));

        // Act
        var result = await _tracker.GetJobStatusAsync(jobId);

        // Assert
        Assert.Equal(status, result.Status);
    }

    [Fact]
    public async Task WatchJobAsync_ShouldSubscribeToCorrectChannel()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var expectedChannel = $"job:updates:{jobId}";

        // Setup subscriber to immediately complete with a terminal update
        _mockSubscriber.Setup(sub => sub.SubscribeAsync(
                It.IsAny<RedisChannel>(),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisChannel, Action<RedisChannel, RedisValue>, CommandFlags>(
                (_, handler, _) =>
                {
                    // Simulate a terminal status immediately
                    var update = new JobStatusUpdate(jobId, ProcessingStatus.Completed, 100, "Done", DateTimeOffset.UtcNow);
                    handler(RedisChannel.Literal(expectedChannel), JsonSerializer.Serialize(update));
                })
            .Returns(Task.CompletedTask);

        _mockSubscriber.Setup(sub => sub.UnsubscribeAsync(
                It.IsAny<RedisChannel>(),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                It.IsAny<CommandFlags>()))
            .Returns(Task.CompletedTask);

        // Act
        var updates = new List<JobStatusUpdate>();
        await foreach (var update in _tracker.WatchJobAsync(jobId))
        {
            updates.Add(update);
        }

        // Assert
        _mockSubscriber.Verify(sub => sub.SubscribeAsync(
            It.Is<RedisChannel>(ch => ch.ToString() == expectedChannel),
            It.IsAny<Action<RedisChannel, RedisValue>>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task WatchJobAsync_ShouldYieldUpdates()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        Action<RedisChannel, RedisValue>? capturedHandler = null;

        _mockSubscriber.Setup(sub => sub.SubscribeAsync(
                It.IsAny<RedisChannel>(),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisChannel, Action<RedisChannel, RedisValue>, CommandFlags>(
                (_, handler, _) =>
                {
                    capturedHandler = handler;
                    // Send two updates: one in-progress, one completed
                    var update1 = new JobStatusUpdate(jobId, ProcessingStatus.Processing, 50, "Half", DateTimeOffset.UtcNow);
                    var update2 = new JobStatusUpdate(jobId, ProcessingStatus.Completed, 100, "Done", DateTimeOffset.UtcNow);
                    handler(RedisChannel.Literal($"job:updates:{jobId}"), JsonSerializer.Serialize(update1));
                    handler(RedisChannel.Literal($"job:updates:{jobId}"), JsonSerializer.Serialize(update2));
                })
            .Returns(Task.CompletedTask);

        _mockSubscriber.Setup(sub => sub.UnsubscribeAsync(
                It.IsAny<RedisChannel>(),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                It.IsAny<CommandFlags>()))
            .Returns(Task.CompletedTask);

        // Act
        var updates = new List<JobStatusUpdate>();
        await foreach (var update in _tracker.WatchJobAsync(jobId))
        {
            updates.Add(update);
        }

        // Assert
        Assert.Equal(2, updates.Count);
        Assert.Equal(ProcessingStatus.Processing, updates[0].Status);
        Assert.Equal(50, updates[0].ProgressPercent);
        Assert.Equal(ProcessingStatus.Completed, updates[1].Status);
        Assert.Equal(100, updates[1].ProgressPercent);
    }

    [Fact]
    public async Task WatchJobAsync_WithFailedStatus_ShouldStopStreaming()
    {
        // Arrange
        var jobId = Guid.NewGuid();

        _mockSubscriber.Setup(sub => sub.SubscribeAsync(
                It.IsAny<RedisChannel>(),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisChannel, Action<RedisChannel, RedisValue>, CommandFlags>(
                (_, handler, _) =>
                {
                    var update = new JobStatusUpdate(jobId, ProcessingStatus.Failed, 30, "Error", DateTimeOffset.UtcNow);
                    handler(RedisChannel.Literal($"job:updates:{jobId}"), JsonSerializer.Serialize(update));
                })
            .Returns(Task.CompletedTask);

        _mockSubscriber.Setup(sub => sub.UnsubscribeAsync(
                It.IsAny<RedisChannel>(),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                It.IsAny<CommandFlags>()))
            .Returns(Task.CompletedTask);

        // Act
        var updates = new List<JobStatusUpdate>();
        await foreach (var update in _tracker.WatchJobAsync(jobId))
        {
            updates.Add(update);
        }

        // Assert
        Assert.Single(updates);
        Assert.Equal(ProcessingStatus.Failed, updates[0].Status);
    }

    [Fact]
    public async Task WatchJobAsync_WithCancelledStatus_ShouldStopStreaming()
    {
        // Arrange
        var jobId = Guid.NewGuid();

        _mockSubscriber.Setup(sub => sub.SubscribeAsync(
                It.IsAny<RedisChannel>(),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisChannel, Action<RedisChannel, RedisValue>, CommandFlags>(
                (_, handler, _) =>
                {
                    var update = new JobStatusUpdate(jobId, ProcessingStatus.Cancelled, 0, "Cancelled", DateTimeOffset.UtcNow);
                    handler(RedisChannel.Literal($"job:updates:{jobId}"), JsonSerializer.Serialize(update));
                })
            .Returns(Task.CompletedTask);

        _mockSubscriber.Setup(sub => sub.UnsubscribeAsync(
                It.IsAny<RedisChannel>(),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                It.IsAny<CommandFlags>()))
            .Returns(Task.CompletedTask);

        // Act
        var updates = new List<JobStatusUpdate>();
        await foreach (var update in _tracker.WatchJobAsync(jobId))
        {
            updates.Add(update);
        }

        // Assert
        Assert.Single(updates);
        Assert.Equal(ProcessingStatus.Cancelled, updates[0].Status);
    }

    [Fact]
    public async Task WatchJobAsync_ShouldUnsubscribeAfterCompletion()
    {
        // Arrange
        var jobId = Guid.NewGuid();

        _mockSubscriber.Setup(sub => sub.SubscribeAsync(
                It.IsAny<RedisChannel>(),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisChannel, Action<RedisChannel, RedisValue>, CommandFlags>(
                (_, handler, _) =>
                {
                    var update = new JobStatusUpdate(jobId, ProcessingStatus.Completed, 100, "Done", DateTimeOffset.UtcNow);
                    handler(RedisChannel.Literal($"job:updates:{jobId}"), JsonSerializer.Serialize(update));
                })
            .Returns(Task.CompletedTask);

        _mockSubscriber.Setup(sub => sub.UnsubscribeAsync(
                It.IsAny<RedisChannel>(),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                It.IsAny<CommandFlags>()))
            .Returns(Task.CompletedTask);

        // Act
        await foreach (var _ in _tracker.WatchJobAsync(jobId)) { }

        // Assert
        _mockSubscriber.Verify(sub => sub.UnsubscribeAsync(
            It.Is<RedisChannel>(ch => ch.ToString() == $"job:updates:{jobId}"),
            It.IsAny<Action<RedisChannel, RedisValue>>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }
}
