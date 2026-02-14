using System.Text.Json;
using System.Threading.Channels;
using InfoDumpManager.Application.Agents.Orchestration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace InfoDumpManager.Infrastructure.Services;

/// <summary>
/// Redis-backed job status tracker using hash keys for durability and Pub/Sub for streaming updates.
/// </summary>
public sealed class RedisJobTracker : IJobTracker
{
    private const string KeyPrefix = "job:status:";
    private const string ChannelPrefix = "job:updates:";

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisJobTracker> _logger;

    public RedisJobTracker(IConnectionMultiplexer redis, ILogger<RedisJobTracker> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public void UpdateStatus(Guid jobId, ProcessingStatus status, int progress, string message)
    {
        var snapshot = new JobStatus(jobId, status, progress, message, DateTimeOffset.UtcNow);
        var db = _redis.GetDatabase();
        var key = KeyPrefix + jobId;
        var json = JsonSerializer.Serialize(snapshot);

        // Fire-and-forget for performance — status is best-effort
        db.StringSet(key, json, TimeSpan.FromHours(24), flags: CommandFlags.FireAndForget);

        // Publish update for any watchers
        var update = new JobStatusUpdate(jobId, status, progress, message, DateTimeOffset.UtcNow);
        var sub = _redis.GetSubscriber();
        sub.Publish(RedisChannel.Literal(ChannelPrefix + jobId),
            JsonSerializer.Serialize(update), CommandFlags.FireAndForget);
    }

    public async Task<JobStatus> GetJobStatusAsync(Guid jobId)
    {
        var db = _redis.GetDatabase();
        var json = await db.StringGetAsync(KeyPrefix + jobId).ConfigureAwait(false);

        if (json.IsNullOrEmpty)
        {
            return new JobStatus(jobId, ProcessingStatus.Pending, 0, "Pending", DateTimeOffset.UtcNow);
        }

        return JsonSerializer.Deserialize<JobStatus>(json!)
            ?? new JobStatus(jobId, ProcessingStatus.Pending, 0, "Pending", DateTimeOffset.UtcNow);
    }

    public async IAsyncEnumerable<JobStatusUpdate> WatchJobAsync(Guid jobId)
    {
        var updateChannel = Channel.CreateUnbounded<JobStatusUpdate>();
        var subscriber = _redis.GetSubscriber();
        var redisChannel = RedisChannel.Literal(ChannelPrefix + jobId);

        await subscriber.SubscribeAsync(redisChannel, (_, value) =>
        {
            if (value.IsNullOrEmpty) return;

            var update = JsonSerializer.Deserialize<JobStatusUpdate>(value!);
            if (update is not null)
            {
                updateChannel.Writer.TryWrite(update);

                // Close channel if terminal status
                if (update.Status is ProcessingStatus.Completed or ProcessingStatus.Failed or ProcessingStatus.Cancelled)
                {
                    updateChannel.Writer.TryComplete();
                }
            }
        }).ConfigureAwait(false);

        await foreach (var update in updateChannel.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            yield return update;
        }

        await subscriber.UnsubscribeAsync(redisChannel).ConfigureAwait(false);
    }
}
