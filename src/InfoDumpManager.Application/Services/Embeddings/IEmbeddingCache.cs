namespace InfoDumpManager.Application.Services.Embeddings;

/// <summary>
/// Cache for embedding vectors.
/// </summary>
public interface IEmbeddingCache
{
    Task<float[]?> TryGetAsync(string cacheKey, CancellationToken cancellationToken = default);

    Task SetAsync(string cacheKey, float[] vector, TimeSpan timeToLive, CancellationToken cancellationToken = default);
}
