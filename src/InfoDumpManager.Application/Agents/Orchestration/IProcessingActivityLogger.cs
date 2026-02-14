namespace InfoDumpManager.Application.Agents.Orchestration;

/// <summary>
/// Writes ActivityLog entries for processing pipeline steps.
/// </summary>
public interface IProcessingActivityLogger
{
    Task LogValidationAsync(Guid tenantId, Guid gemId, AgentResult validation, CancellationToken ct = default);
    Task LogSummarizationAsync(Guid tenantId, Guid gemId, AgentResult summarization, CancellationToken ct = default);
}
