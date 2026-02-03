namespace InfoDumpManager.Application.Services.Embeddings;

/// <summary>
/// Abstraction for embedding generation.
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>
    /// Generates an embedding vector for the provided content.
    /// </summary>
    /// <param name="content">Content to embed.</param>
    /// <param name="model">Embedding model identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Embedding response.</returns>
    Task<EmbeddingResponse> GenerateEmbeddingAsync(
        string content,
        string model,
        CancellationToken cancellationToken = default);
}
