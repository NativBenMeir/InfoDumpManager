using System.Collections.Concurrent;
using System.Threading.Channels;

namespace InfoDumpManager.Application.Agents.Orchestration;

/// <summary>
/// In-memory implementation of job status tracking.
/// </summary>
public sealed class InMemoryJobTracker : IJobTracker
{
    private readonly ConcurrentDictionary<Guid, JobStatus> _jobStatuses = new();
    private readonly ConcurrentDictionary<Guid, Channel<JobStatusUpdate>> _statusChannels = new();

    public void UpdateStatus(Guid jobId, ProcessingStatus status, int progress, string message)
    {
        var snapshot = new JobStatus(jobId, status, progress, message, DateTimeOffset.UtcNow);
        _jobStatuses.AddOrUpdate(jobId, snapshot, (_, _) => snapshot);

        if (_statusChannels.TryGetValue(jobId, out var channel))
        {
            channel.Writer.TryWrite(new JobStatusUpdate(jobId, status, progress, message, DateTimeOffset.UtcNow));
        }
    }

    public Task<JobStatus> GetJobStatusAsync(Guid jobId)
    {
        if (_jobStatuses.TryGetValue(jobId, out var status))
        {
            return Task.FromResult(status);
        }

        return Task.FromResult(new JobStatus(jobId, ProcessingStatus.Pending, 0, "Pending", DateTimeOffset.UtcNow));
    }

    public IAsyncEnumerable<JobStatusUpdate> WatchJobAsync(Guid jobId)
    {
        var channel = _statusChannels.GetOrAdd(jobId, _ => Channel.CreateUnbounded<JobStatusUpdate>());
        return channel.Reader.ReadAllAsync();
    }
}
