using InfoDumpManager.Application.Agents.Orchestration;

namespace InfoDumpManager.Application.Infrastructure.JobQueue;

/// <summary>
/// Processing job for AI pipeline execution.
/// </summary>
/// <param name="JobId">Job identifier.</param>
/// <param name="GEMId">GEM identifier.</param>
/// <param name="TenantId">Tenant identifier.</param>
/// <param name="ContentText">Content to process.</param>
/// <param name="Options">Processing options.</param>
/// <param name="RetryCount">Retry count.</param>
/// <param name="CreatedAt">Creation time.</param>
/// <param name="StartedAt">Start time.</param>
public sealed record ProcessingJob(
    Guid JobId,
    Guid GEMId,
    Guid TenantId,
    string ContentText,
    ProcessingOptions Options,
    int RetryCount = 0,
    DateTimeOffset CreatedAt = default,
    DateTimeOffset? StartedAt = null);
