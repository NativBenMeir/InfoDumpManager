using InfoDumpManager.Domain.ValueObjects;

namespace InfoDumpManager.Application.Agents.Orchestration;

/// <summary>
/// Coordinates multi-agent processing for GEM content.
/// </summary>
public interface IContentProcessingOrchestrator
{
    /// <summary>
    /// Processes a single GEM through the agent pipeline.
    /// </summary>
    Task<ProcessingResult> ProcessGEMAsync(
        Guid gemId,
        Guid tenantId,
        string contentText,
        ProcessingOptions options,
        Guid? jobId = null);

    /// <summary>
    /// Processes a batch of GEMs through the agent pipeline.
    /// </summary>
    Task<ProcessingResult> ProcessBatchAsync(
        IEnumerable<(Guid GEMId, Guid TenantId, string ContentText)> items,
        ProcessingOptions options);

    /// <summary>
    /// Gets the current status for a processing job.
    /// </summary>
    Task<JobStatus> GetJobStatusAsync(Guid jobId);

    /// <summary>
    /// Streams status updates for a processing job.
    /// </summary>
    IAsyncEnumerable<JobStatusUpdate> WatchJobAsync(Guid jobId);
}

/// <summary>
/// Processing result for a GEM.
/// </summary>
/// <param name="GEMId">GEM identifier.</param>
/// <param name="Status">Current processing status.</param>
/// <param name="Summary">Generated summary.</param>
/// <param name="Summarization">Summarization agent output.</param>
/// <param name="Categorization">Categorization agent output.</param>
/// <param name="Tagging">Tagging agent output.</param>
/// <param name="Validation">Validation agent output.</param>
/// <param name="Errors">Errors encountered.</param>
/// <param name="CompletedAt">Completion timestamp.</param>
public sealed record ProcessingResult(
    Guid GEMId,
    ProcessingStatus Status,
    GEMSummary? Summary,
    AgentResult? Summarization,
    AgentResult? Categorization,
    AgentResult? Tagging,
    AgentResult? Validation,
    List<string> Errors,
    DateTimeOffset CompletedAt);

/// <summary>
/// Processing lifecycle status.
/// </summary>
public enum ProcessingStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Options for processing pipeline execution.
/// </summary>
/// <param name="Source">Content source identifier.</param>
/// <param name="AutoApproveThreshold">Confidence threshold for auto-approval.</param>
/// <param name="RunValidation">Whether validation should run.</param>
/// <param name="MaxConcurrentJobs">Maximum concurrent jobs for batch processing.</param>
/// <param name="Timeout">Optional processing timeout.</param>
public sealed record ProcessingOptions(
    string Source = "web",
    double AutoApproveThreshold = 0.8,
    bool RunValidation = true,
    int? MaxConcurrentJobs = null,
    TimeSpan? Timeout = null);

/// <summary>
/// Current job status snapshot.
/// </summary>
/// <param name="JobId">Job identifier.</param>
/// <param name="Status">Current processing status.</param>
/// <param name="ProgressPercent">Progress percentage.</param>
/// <param name="Message">Optional status message.</param>
/// <param name="UpdatedAt">Last update timestamp.</param>
public sealed record JobStatus(
    Guid JobId,
    ProcessingStatus Status,
    int ProgressPercent,
    string? Message,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Streaming status update for a job.
/// </summary>
/// <param name="JobId">Job identifier.</param>
/// <param name="Status">Current processing status.</param>
/// <param name="ProgressPercent">Progress percentage.</param>
/// <param name="Message">Optional status message.</param>
/// <param name="UpdatedAt">Last update timestamp.</param>
public sealed record JobStatusUpdate(
    Guid JobId,
    ProcessingStatus Status,
    int ProgressPercent,
    string? Message,
    DateTimeOffset UpdatedAt);
