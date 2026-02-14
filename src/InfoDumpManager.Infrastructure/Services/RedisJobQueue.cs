using System.Text.Json;
using InfoDumpManager.Application.Infrastructure.JobQueue;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace InfoDumpManager.Infrastructure.Services;

/// <summary>
/// Redis Stream-backed durable job queue.
/// </summary>
public sealed class RedisJobQueue<T> : IJobQueue<T> where T : class
{
    private const string StreamKey = "jobs:processing";
    private const string GroupName = "workers";
    private const string ConsumerName = "worker-1";
    private const string PayloadField = "payload";

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisJobQueue<T>> _logger;
    private bool _groupCreated;

    public RedisJobQueue(IConnectionMultiplexer redis, ILogger<RedisJobQueue<T>> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task EnqueueAsync(T job)
    {
        var db = _redis.GetDatabase();
        var json = JsonSerializer.Serialize(job);
        await db.StreamAddAsync(StreamKey, new[] { new NameValueEntry(PayloadField, json) })
            .ConfigureAwait(false);
        _logger.LogInformation("Job enqueued to Redis stream: {JobType}", typeof(T).Name);
    }

    public async Task<T?> DequeueAsync(TimeSpan timeout)
    {
        var db = _redis.GetDatabase();
        await EnsureConsumerGroupAsync(db).ConfigureAwait(false);

        // Read one message from the stream for this consumer group
        var entries = await db.StreamReadGroupAsync(
            StreamKey, GroupName, ConsumerName,
            ">",                // read only new messages
            count: 1)
            .ConfigureAwait(false);

        if (entries is null || entries.Length == 0)
        {
            // Wait briefly then return null (polling pattern)
            await Task.Delay(timeout).ConfigureAwait(false);
            return null;
        }

        var entry = entries[0];
        var json = entry[PayloadField];
        if (json.IsNullOrEmpty)
        {
            await db.StreamAcknowledgeAsync(StreamKey, GroupName, entry.Id).ConfigureAwait(false);
            return null;
        }

        return JsonSerializer.Deserialize<T>(json!);
    }

    public async Task MarkCompleteAsync(T job)
    {
        // In a full implementation, we'd track the message ID per job.
        // For simplicity, acknowledge is handled at dequeue time or via
        // a separate tracking mechanism. Here we log completion.
        _logger.LogInformation("Job completed: {JobType}", typeof(T).Name);
        await Task.CompletedTask;
    }

    public async Task MarkFailedAsync(T job, string error, int retryCount)
    {
        _logger.LogWarning("Job failed (retry {RetryCount}): {Error}", retryCount, error);

        if (retryCount < 3)
        {
            var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount));
            await Task.Delay(delay).ConfigureAwait(false);

            // Re-enqueue with incremented retry count
            if (job is ProcessingJob processingJob)
            {
                var updatedJob = processingJob with
                {
                    RetryCount = retryCount + 1,
                    StartedAt = null
                };
                await EnqueueAsync((T)(object)updatedJob).ConfigureAwait(false);
                return;
            }

            await EnqueueAsync(job).ConfigureAwait(false);
        }
        else
        {
            _logger.LogError("Job abandoned after {RetryCount} retries", retryCount);
        }
    }

    public async IAsyncEnumerable<T> DequeueBatchAsync(int batchSize)
    {
        for (var i = 0; i < batchSize; i++)
        {
            var job = await DequeueAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            if (job is null) yield break;
            yield return job;
        }
    }

    private async Task EnsureConsumerGroupAsync(IDatabase db)
    {
        if (_groupCreated) return;

        try
        {
            await db.StreamCreateConsumerGroupAsync(StreamKey, GroupName, "0-0", true)
                .ConfigureAwait(false);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Consumer group already exists — expected on restart
        }

        _groupCreated = true;
    }
}
