namespace InfoDumpManager.Application.Agents;

/// <summary>
/// Defines types of operations agents can perform.
/// </summary>
public enum AgentCapability
{
    Summarization,
    Categorization,
    Tagging,
    Validation,
    CostManagement,
    Orchestration
}

/// <summary>
/// Input context for agent execution.
/// </summary>
/// <param name="GEMId">Target GEM identifier.</param>
/// <param name="TenantId">Tenant identifier.</param>
/// <param name="ContentText">Content to process.</param>
/// <param name="Metadata">Execution metadata.</param>
public sealed record AgentContext(
    Guid GEMId,
    Guid TenantId,
    string ContentText,
    AgentContextMetadata Metadata);

/// <summary>
/// Metadata passed to agent execution.
/// </summary>
/// <param name="ContentSource">Origin of the content.</param>
/// <param name="EstimatedTokenCount">Estimated token count.</param>
/// <param name="CreatedAt">Timestamp for context creation.</param>
/// <param name="CustomData">Additional custom metadata.</param>
public sealed record AgentContextMetadata(
    string ContentSource,
    int EstimatedTokenCount,
    DateTimeOffset CreatedAt,
    Dictionary<string, object> CustomData);

/// <summary>
/// Standardized output from agent execution.
/// </summary>
/// <param name="Success">Indicates whether the operation succeeded.</param>
/// <param name="Message">Outcome message.</param>
/// <param name="Data">Execution data payload.</param>
/// <param name="Metrics">Execution metrics.</param>
/// <param name="Errors">Optional error list.</param>
/// <param name="Confidence">Optional confidence metadata.</param>
public sealed record AgentResult(
    bool Success,
    string Message,
    AgentResultData Data,
    AgentMetrics Metrics,
    List<string>? Errors = null,
    AgentResultConfidence? Confidence = null);

/// <summary>
/// Data payload for agent execution.
/// </summary>
/// <param name="AgentName">Agent name.</param>
/// <param name="ExecutedAt">Execution timestamp.</param>
/// <param name="Payload">Arbitrary data payload.</param>
public sealed record AgentResultData(
    string AgentName,
    DateTimeOffset ExecutedAt,
    Dictionary<string, object> Payload);

/// <summary>
/// Execution metrics for an agent call.
/// </summary>
/// <param name="TokensUsed">Tokens consumed by the provider.</param>
/// <param name="EstimatedCost">Estimated cost for the call.</param>
/// <param name="ExecutionTime">Elapsed execution time.</param>
/// <param name="RetryCount">Retry count applied by resilience policies.</param>
/// <param name="ProviderUsed">Provider name.</param>
public sealed record AgentMetrics(
    int TokensUsed,
    decimal EstimatedCost,
    TimeSpan ExecutionTime,
    int RetryCount,
    string ProviderUsed);

/// <summary>
/// Confidence information for agent output.
/// </summary>
/// <param name="Score">Confidence score from 0.0 to 1.0.</param>
/// <param name="RequiresManualReview">Whether manual review is required.</param>
/// <param name="Reasoning">Confidence reasoning.</param>
public sealed record AgentResultConfidence(
    double Score,
    bool RequiresManualReview,
    string Reasoning);

/// <summary>
/// Tag suggestion output from tagging agent.
/// </summary>
/// <param name="TagId">Tag identifier.</param>
/// <param name="TagName">Tag name.</param>
/// <param name="SimilarityScore">Similarity score from 0.0 to 1.0.</param>
public sealed record TagSuggestionResult(
    Guid TagId,
    string TagName,
    double SimilarityScore);
