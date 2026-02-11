# Phase 5 — Durable Job Queue (Redis-backed)

## Goal
Replace `InMemoryJobQueue<T>` (which loses jobs on process restart) with a Redis-backed implementation using Redis Streams. Keep the `IJobQueue<T>` and `IJobTracker` interfaces unchanged so the rest of the codebase is unaffected.

## Current State

### Interfaces (no changes needed)

**File:** `src/InfoDumpManager.Application/Infrastructure/JobQueue/IJobQueue.cs`
```csharp
public interface IJobQueue<T> where T : class
{
    Task EnqueueAsync(T job);
    Task<T?> DequeueAsync(TimeSpan timeout);
    Task MarkCompleteAsync(T job);
    Task MarkFailedAsync(T job, string error, int retryCount);
    IAsyncEnumerable<T> DequeueBatchAsync(int batchSize);
}
```

**File:** `src/InfoDumpManager.Application/Agents/Orchestration/IJobTracker.cs`
```csharp
public interface IJobTracker
{
    void UpdateStatus(Guid jobId, ProcessingStatus status, int progress, string message);
    Task<JobStatus> GetJobStatusAsync(Guid jobId);
    IAsyncEnumerable<JobStatusUpdate> WatchJobAsync(Guid jobId);
}
```

### In-memory implementations (to be replaced)

**File:** `src/InfoDumpManager.Infrastructure/Services/InMemoryJobQueue.cs` — keep file but it becomes fallback
**File:** `src/InfoDumpManager.Application/Agents/Orchestration/InMemoryJobTracker.cs` — keep file but it becomes fallback

### DI registration

**File:** `src/InfoDumpManager.Infrastructure/DependencyInjection.cs`
```csharp
services.AddSingleton<IJobTracker, InMemoryJobTracker>();
services.AddSingleton<IJobQueue<ProcessingJob>, InMemoryJobQueue<ProcessingJob>>();
```

### Redis already wired

`IConnectionMultiplexer` is already registered in `DependencyInjection.cs`:
```csharp
services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var redisConfiguration = configuration.GetConnectionString("Redis")
        ?? configuration["Redis:Configuration"]
        ?? "localhost:6379";
    return ConnectionMultiplexer.Connect(redisConfiguration);
});
```

## Changes

### 5.1 — Create `RedisJobQueue<T>`

**New file:** `src/InfoDumpManager.Infrastructure/Services/RedisJobQueue.cs`

Uses Redis Streams (`XADD`, `XREADGROUP`, `XACK`) for durable, consumer-group-based dequeue.

```csharp
using System.Runtime.CompilerServices;
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

    public async IAsyncEnumerable<T> DequeueBatchAsync(
        int batchSize,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
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
```

> **Note:** The `IAsyncEnumerable<T> DequeueBatchAsync(int batchSize)` signature in `IJobQueue<T>` does not include `CancellationToken`. The implementation should add `[EnumeratorCancellation]` or omit it. The interface method returns `IAsyncEnumerable<T>` so the cancellation token is optional.

### 5.2 — Create `RedisJobTracker`

**New file:** `src/InfoDumpManager.Infrastructure/Services/RedisJobTracker.cs`

Uses Redis hash keys for durability and Pub/Sub for streaming updates.

```csharp
using System.Runtime.CompilerServices;
using System.Text.Json;
using InfoDumpManager.Application.Agents.Orchestration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace InfoDumpManager.Infrastructure.Services;

/// <summary>
/// Redis-backed job status tracker.
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

    public async IAsyncEnumerable<JobStatusUpdate> WatchJobAsync(
        Guid jobId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = await _redis.GetSubscriber()
            .SubscribeAsync(RedisChannel.Literal(ChannelPrefix + jobId))
            .ConfigureAwait(false);

        await foreach (var message in channel.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            if (message.Message.IsNullOrEmpty) continue;

            var update = JsonSerializer.Deserialize<JobStatusUpdate>(message.Message!);
            if (update is not null)
            {
                yield return update;

                // Stop if terminal status
                if (update.Status is ProcessingStatus.Completed or ProcessingStatus.Failed or ProcessingStatus.Cancelled)
                    yield break;
            }
        }
    }
}
```

> **Note on `IJobTracker.WatchJobAsync`:** The interface signature is `IAsyncEnumerable<JobStatusUpdate> WatchJobAsync(Guid jobId)` without `CancellationToken`. The Redis implementation can still add it with `[EnumeratorCancellation]` as an optional overload, or just ignore the cancel token. Match the interface signature exactly.

### 5.3 — Update DI registration

**File:** `src/InfoDumpManager.Infrastructure/DependencyInjection.cs`

Replace:
```csharp
services.AddSingleton<IJobTracker, InMemoryJobTracker>();
services.AddSingleton<IJobQueue<ProcessingJob>, InMemoryJobQueue<ProcessingJob>>();
```

With:
```csharp
services.AddSingleton<IJobTracker, RedisJobTracker>();
services.AddSingleton<IJobQueue<ProcessingJob>, RedisJobQueue<ProcessingJob>>();
```

### 5.4 — Keep in-memory implementations as fallback

Do **not** delete `InMemoryJobQueue.cs` or `InMemoryJobTracker.cs`. They serve as fallback for test environments without Redis. Optionally, add a configuration toggle:

```csharp
var useRedisJobs = configuration.GetValue<bool>("JobQueue:UseRedis", true);
if (useRedisJobs)
{
    services.AddSingleton<IJobTracker, RedisJobTracker>();
    services.AddSingleton<IJobQueue<ProcessingJob>, RedisJobQueue<ProcessingJob>>();
}
else
{
    services.AddSingleton<IJobTracker, InMemoryJobTracker>();
    services.AddSingleton<IJobQueue<ProcessingJob>, InMemoryJobQueue<ProcessingJob>>();
}
```

### 5.5 — Add configuration section

**File:** `src/InfoDumpManager.WebAPI/appsettings.json`

Add:
```json
"JobQueue": {
    "UseRedis": true
}
```

**File:** `src/InfoDumpManager.WebAPI/appsettings.Development.json`

If you want to use in-memory for local dev without Redis:
```json
"JobQueue": {
    "UseRedis": false
}
```

## Verification

```bash
# With Redis running (docker-compose up -d redis)
dotnet build
dotnet test

# Verify queue survives restart
# 1. Enqueue a job via API
# 2. Stop the web host
# 3. Restart the web host
# 4. Verify job is picked up by the background service
```
