namespace InfoDumpManager.Application.Services.Embeddings;

/// <summary>
/// Response from an embedding provider.
/// </summary>
/// <param name="Vector">Embedding vector.</param>
/// <param name="Model">Model used for embedding.</param>
/// <param name="Provider">Provider name.</param>
/// <param name="TokensUsed">Tokens used.</param>
/// <param name="CostEstimate">Estimated cost in USD.</param>
public sealed record EmbeddingResponse(
    float[] Vector,
    string Model,
    string Provider,
    int TokensUsed,
    decimal CostEstimate);
