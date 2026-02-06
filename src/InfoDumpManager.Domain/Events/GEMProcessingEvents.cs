namespace InfoDumpManager.Domain.Events;

/// <summary>
/// Base domain event for AI processing lifecycle.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Gets the time the event occurred.
    /// </summary>
    DateTimeOffset OccurredAt { get; }
}

/// <summary>
/// Emitted when a GEM is created and queued for processing.
/// </summary>
public sealed record GEMCreatedAndQueuedForProcessing(
    Guid GEMId,
    Guid TenantId,
    string Title,
    DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>
/// Emitted when summarization starts.
/// </summary>
public sealed record GEMSummarizationStarted(
    Guid GEMId,
    Guid TenantId,
    DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>
/// Emitted when summarization completes.
/// </summary>
public sealed record GEMSummarizationCompleted(
    Guid GEMId,
    Guid TenantId,
    string Summary,
    int TokensUsed,
    DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>
/// Emitted when categorization is suggested.
/// </summary>
public sealed record GEMCategorizationSuggested(
    Guid GEMId,
    Guid? CategoryId,
    double ConfidenceScore,
    bool RequiresManualReview,
    DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>
/// Tag suggestion detail for event payload.
/// </summary>
public sealed record TagSuggestionDetail(
    Guid TagId,
    string TagName,
    double SimilarityScore);

/// <summary>
/// Emitted when tags are suggested.
/// </summary>
public sealed record GEMTaggingSuggested(
    Guid GEMId,
    Guid TenantId,
    IReadOnlyList<TagSuggestionDetail> Tags,
    DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>
/// Emitted when processing completes.
/// </summary>
public sealed record GEMProcessingCompleted(
    Guid GEMId,
    Guid TenantId,
    ProcessingStatus Status,
    DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>
/// Emitted when processing fails.
/// </summary>
public sealed record GEMProcessingFailed(
    Guid GEMId,
    Guid TenantId,
    List<string> Errors,
    DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>
/// Emitted when a user rejects a suggested category.
/// </summary>
public sealed record CategorySuggestionRejectedByUser(
    Guid GEMId,
    Guid? SuggestedCategoryId,
    Guid? ActualCategoryId,
    DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>
/// Processing lifecycle status for domain events.
/// </summary>
public enum ProcessingStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Cancelled
}
