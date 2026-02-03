namespace InfoDumpManager.Application.Services.Embeddings;

/// <summary>
/// Vector store abstraction for embeddings.
/// </summary>
public interface IVectorStore
{
    Task StoreAsync(EmbeddingRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmbeddingSearchResult>> SearchSimilarAsync(
        EmbeddingSearchRequest request,
        CancellationToken cancellationToken = default);
}
