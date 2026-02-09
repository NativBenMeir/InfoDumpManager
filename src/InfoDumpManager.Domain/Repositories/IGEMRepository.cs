using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InfoDumpManager.Domain.Entities;

namespace InfoDumpManager.Domain.Repositories;

public interface IGEMRepository
{
    Task AddAsync(GEM gem, CancellationToken cancellationToken = default);
    Task<GEM?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<GEM?> GetByUrlAsync(Guid tenantId, string url, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<GEM>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<GEM>> ListByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByUrlAsync(Guid tenantId, string url, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Search GEMs by semantic similarity using vector embeddings.
    /// </summary>
    Task<IReadOnlyList<(GEM Gem, float Distance)>> SearchBySemanticSimilarityAsync(
        Guid tenantId,
        float[] queryEmbedding,
        int topK = 10,
        float maxDistance = 1.0f,
        Guid? categoryFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Search GEMs by full-text keyword matching.
    /// </summary>
    Task<IReadOnlyList<(GEM Gem, float Rank)>> SearchByFullTextAsync(
        Guid tenantId,
        string searchQuery,
        int topK = 10,
        Guid? categoryFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Search GEMs using hybrid approach combining full-text and semantic similarity.
    /// </summary>
    Task<IReadOnlyList<(GEM Gem, float RelevanceScore)>> SearchHybridAsync(
        Guid tenantId,
        string searchQuery,
        float[] queryEmbedding,
        float textWeight = 0.4f,
        float vectorWeight = 0.6f,
        int topK = 10,
        Guid? categoryFilter = null,
        CancellationToken cancellationToken = default);
}
