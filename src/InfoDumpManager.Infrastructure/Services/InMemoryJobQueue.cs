using System.Threading.Channels;
using InfoDumpManager.Application.Infrastructure.JobQueue;
using Microsoft.Extensions.Logging;

namespace InfoDumpManager.Infrastructure.Services;

/// <summary>
/// In-memory job queue for background processing.
/// </summary>
/// <typeparam name="T">Job type.</typeparam>
public sealed class InMemoryJobQueue<T> : IJobQueue<T>
    where T : class
{
    private readonly Channel<T> _channel;
    private readonly ILogger<InMemoryJobQueue<T>> _logger;

    public InMemoryJobQueue(ILogger<InMemoryJobQueue<T>> logger)
    {
        _channel = Channel.CreateUnbounded<T>();
        _logger = logger;
    }

    public async Task EnqueueAsync(T job)
    {
        await _channel.Writer.WriteAsync(job);
        _logger.LogInformation("Job enqueued: {JobType}", typeof(T).Name);
    }

    public async Task<T?> DequeueAsync(TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);

        try
        {
            return await _channel.Reader.ReadAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    public Task MarkCompleteAsync(T job)
    {
        _logger.LogInformation("Job completed: {JobType}", typeof(T).Name);
        return Task.CompletedTask;
    }

    public async Task MarkFailedAsync(T job, string error, int retryCount)
    {
        _logger.LogWarning("Job failed (retry {RetryCount}): {Error}", retryCount, error);

        if (retryCount < 3)
        {
            var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount));
            await Task.Delay(delay);

            if (job is ProcessingJob processingJob)
            {
                var updatedJob = processingJob with
                {
                    RetryCount = retryCount + 1,
                    StartedAt = null
                };

                await EnqueueAsync((T)(object)updatedJob);
                return;
            }

            await EnqueueAsync(job);
        }
        else
        {
            _logger.LogError("Job abandoned after {RetryCount} retries", retryCount);
        }
    }

    public async IAsyncEnumerable<T> DequeueBatchAsync(int batchSize)
    {
        for (var index = 0; index < batchSize; index++)
        {
            var job = await DequeueAsync(TimeSpan.FromSeconds(5));

            if (job is null)
            {
                yield break;
            }

            yield return job;
        }
    }
}
