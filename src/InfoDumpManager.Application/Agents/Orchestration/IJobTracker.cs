namespace InfoDumpManager.Application.Agents.Orchestration;

/// <summary>
/// Tracks processing job status and provides streaming updates.
/// </summary>
public interface IJobTracker
{
    void UpdateStatus(Guid jobId, ProcessingStatus status, int progress, string message);
    Task<JobStatus> GetJobStatusAsync(Guid jobId);
    IAsyncEnumerable<JobStatusUpdate> WatchJobAsync(Guid jobId);
}
