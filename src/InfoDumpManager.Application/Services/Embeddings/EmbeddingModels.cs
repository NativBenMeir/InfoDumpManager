namespace InfoDumpManager.Application.Services.Embeddings;

/// <summary>
/// Embedding record for persistence.
/// </summary>
/// <param name="Id">Embedding identifier.</param>
/// <param name="TenantId">Tenant identifier.</param>
/// <param name="SourceId">Source entity identifier.</param>
/// <param name="ContentType">Content type for filtering.</param>
/// <param name="Model">Embedding model used.</param>
/// <param name="Vector">Embedding vector.</param>
/// <param name="Metadata">Optional metadata as JSON.</param>
/// <param name="CreatedAt">Creation timestamp.</param>
public sealed record EmbeddingRecord(
    Guid Id,
    Guid TenantId,
    Guid SourceId,
    string ContentType,
    string Model,
    float[] Vector,
    string? Metadata,
    DateTimeOffset CreatedAt);

/// <summary>
/// Search request for similar embeddings.
/// </summary>
/// <param name="TenantId">Tenant identifier.</param>
/// <param name="ContentType">Content type filter.</param>
/// <param name="QueryVector">Query vector.</param>
/// <param name="Limit">Max number of results.</param>
public sealed record EmbeddingSearchRequest(
    Guid TenantId,
    string ContentType,
    float[] QueryVector,
    int Limit);

/// <summary>
/// Result from a vector similarity search.
/// </summary>
/// <param name="SourceId">Source entity identifier.</param>
/// <param name="Distance">Vector distance (lower is more similar).</param>
/// <param name="Metadata">Optional metadata as JSON.</param>
public sealed record EmbeddingSearchResult(
    Guid SourceId,
    double Distance,
    string? Metadata);
