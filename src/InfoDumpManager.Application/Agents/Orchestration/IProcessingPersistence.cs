using InfoDumpManager.Domain.ValueObjects;

namespace InfoDumpManager.Application.Agents.Orchestration;

/// <summary>
/// Handles persistence side-effects during agent processing.
/// </summary>
public interface IProcessingPersistence
{
    /// <summary>
    /// Persists a generated summary onto the GEM entity.
    /// </summary>
    Task PersistSummaryAsync(Guid gemId, GEMSummary? summary, CancellationToken ct = default);

    /// <summary>
    /// Creates a CategorySuggestion entity and optionally auto-assigns the category to the GEM.
    /// </summary>
    Task HandleCategorizationAsync(
        Guid tenantId,
        Guid gemId,
        AgentResult categorization,
        ProcessingOptions options,
        CancellationToken ct = default);

    /// <summary>
    /// Logs tagging suggestions for the GEM.
    /// </summary>
    Task HandleTaggingAsync(
        Guid tenantId,
        Guid gemId,
        AgentResult tagging,
        CancellationToken ct = default);
}
