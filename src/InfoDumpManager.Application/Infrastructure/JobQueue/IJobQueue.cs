namespace InfoDumpManager.Application.Infrastructure.JobQueue;

/// <summary>
/// Job queue abstraction for background processing.
/// </summary>
/// <typeparam name="T">Job type.</typeparam>
public interface IJobQueue<T>
    where T : class
{
    Task EnqueueAsync(T job);
    Task<T?> DequeueAsync(TimeSpan timeout);
    Task MarkCompleteAsync(T job);
    Task MarkFailedAsync(T job, string error, int retryCount);
    IAsyncEnumerable<T> DequeueBatchAsync(int batchSize);
}
