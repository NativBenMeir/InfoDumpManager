using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InfoDumpManager.Infrastructure.Repositories;

public sealed class GEMRepository : IGEMRepository
{
    private readonly ApplicationDbContext _context;

    public GEMRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(GEM gem, CancellationToken cancellationToken = default)
    {
        if (gem is null)
        {
            throw new ArgumentNullException(nameof(gem));
        }

        await _context.Gems.AddAsync(gem, cancellationToken).ConfigureAwait(false);
    }

    public Task<GEM?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.Gems
            .Include(x => x.Category)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<GEM?> GetByUrlAsync(Guid tenantId, string url, CancellationToken cancellationToken = default)
    {
        return _context.Gems
            .Include(x => x.Category)
            .SingleOrDefaultAsync(
                x => x.TenantId == tenantId && x.Url == url,
                cancellationToken);
    }

    public async Task<IReadOnlyCollection<GEM>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var gems = await _context.Gems
            .Where(x => x.TenantId == tenantId)
            .Include(x => x.Category)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return gems;
    }

    public async Task<IReadOnlyCollection<GEM>> ListByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        var gems = await _context.Gems
            .Where(x => x.CategoryId == categoryId)
            .Include(x => x.Category)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return gems;
    }

    public Task<bool> ExistsByUrlAsync(Guid tenantId, string url, CancellationToken cancellationToken = default)
    {
        return _context.Gems.AnyAsync(x => x.TenantId == tenantId && x.Url == url, cancellationToken);
    }

    public async Task<IReadOnlyList<(GEM Gem, float Distance)>> SearchBySemanticSimilarityAsync(
        Guid tenantId,
        float[] queryEmbedding,
        int topK = 10,
        float maxDistance = 1.0f,
        Guid? categoryFilter = null,
        CancellationToken cancellationToken = default)
    {
        if (queryEmbedding is null || queryEmbedding.Length == 0)
        {
            return Array.Empty<(GEM Gem, float Distance)>();
        }

        var baseQuery = _context.Gems
            .AsNoTracking()
            .Include(x => x.Category)
            .Where(x => x.TenantId == tenantId);

        if (categoryFilter.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.CategoryId == categoryFilter.Value);
        }

        var candidates = await baseQuery
            .Where(x => x.SummaryEmbedding != null && x.SummaryEmbedding.Length > 0)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var results = candidates
            .Select(gem => (Gem: gem, Distance: CosineDistance(queryEmbedding, gem.SummaryEmbedding!)))
            .Where(result => result.Distance <= maxDistance)
            .OrderBy(result => result.Distance)
            .Take(topK)
            .ToList();

        return results;
    }

    public async Task<IReadOnlyList<(GEM Gem, float Rank)>> SearchByFullTextAsync(
        Guid tenantId,
        string searchQuery,
        int topK = 10,
        Guid? categoryFilter = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            return Array.Empty<(GEM Gem, float Rank)>();
        }

        var baseQuery = _context.Gems
            .AsNoTracking()
            .Include(x => x.Category)
            .Where(x => x.TenantId == tenantId);

        if (categoryFilter.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.CategoryId == categoryFilter.Value);
        }

        var candidates = await baseQuery.ToListAsync(cancellationToken).ConfigureAwait(false);
        var terms = SplitTerms(searchQuery);

        var results = candidates
            .Select(gem => (Gem: gem, Rank: ScoreText(gem, terms)))
            .Where(result => result.Rank > 0)
            .OrderByDescending(result => result.Rank)
            .Take(topK)
            .ToList();

        return results;
    }

    public async Task<IReadOnlyList<(GEM Gem, float RelevanceScore)>> SearchHybridAsync(
        Guid tenantId,
        string searchQuery,
        float[] queryEmbedding,
        float textWeight = 0.4f,
        float vectorWeight = 0.6f,
        int topK = 10,
        Guid? categoryFilter = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchQuery) && (queryEmbedding is null || queryEmbedding.Length == 0))
        {
            return Array.Empty<(GEM Gem, float RelevanceScore)>();
        }

        var baseQuery = _context.Gems
            .AsNoTracking()
            .Include(x => x.Category)
            .Where(x => x.TenantId == tenantId);

        if (categoryFilter.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.CategoryId == categoryFilter.Value);
        }

        var candidates = await baseQuery.ToListAsync(cancellationToken).ConfigureAwait(false);
        var terms = SplitTerms(searchQuery);

        var scored = candidates
            .Select(gem => new
            {
                Gem = gem,
                TextRank = ScoreText(gem, terms),
                VectorDistance = gem.SummaryEmbedding is { Length: > 0 } && queryEmbedding is { Length: > 0 }
                    ? (float?)CosineDistance(queryEmbedding, gem.SummaryEmbedding)
                    : null
            })
            .ToList();

        var maxTextRank = scored.Max(x => x.TextRank);
        var maxVectorDistance = scored.Where(x => x.VectorDistance.HasValue).Select(x => x.VectorDistance!.Value).DefaultIfEmpty(1.0f).Max();

        var results = scored
            .Select(x =>
            {
                var textScore = maxTextRank > 0 ? x.TextRank / maxTextRank : 0f;
                var vectorScore = x.VectorDistance.HasValue && maxVectorDistance > 0
                    ? 1.0f - Math.Clamp(x.VectorDistance.Value / maxVectorDistance, 0f, 1f)
                    : 0f;
                var relevance = (textWeight * textScore) + (vectorWeight * vectorScore);
                return (x.Gem, RelevanceScore: relevance);
            })
            .Where(result => result.RelevanceScore > 0)
            .OrderByDescending(result => result.RelevanceScore)
            .Take(topK)
            .ToList();

        return results;
    }

    private static float CosineDistance(float[] left, float[] right)
    {
        var similarity = CosineSimilarity(left, right);
        return 1.0f - similarity;
    }

    private static float CosineSimilarity(float[] left, float[] right)
    {
        if (left.Length != right.Length)
        {
            throw new ArgumentException("Embedding vectors must be the same length.");
        }

        double dot = 0;
        double normLeft = 0;
        double normRight = 0;

        for (var i = 0; i < left.Length; i++)
        {
            var l = left[i];
            var r = right[i];
            dot += l * r;
            normLeft += l * l;
            normRight += r * r;
        }

        if (normLeft <= 0 || normRight <= 0)
        {
            return 0f;
        }

        return (float)(dot / (Math.Sqrt(normLeft) * Math.Sqrt(normRight)));
    }

    private static IReadOnlyList<string> SplitTerms(string searchQuery)
    {
        return searchQuery
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => term.Length > 1)
            .ToArray();
    }

    private static float ScoreText(GEM gem, IReadOnlyList<string> terms)
    {
        if (terms.Count == 0)
        {
            return 0f;
        }

        var title = gem.Title ?? string.Empty;
        var summary = gem.Summary?.Text ?? string.Empty;
        var combined = string.Concat(title, ' ', summary).ToLowerInvariant();

        float score = 0f;
        foreach (var term in terms)
        {
            var termLower = term.ToLowerInvariant();
            score += CountOccurrences(combined, termLower);
        }

        return score;
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        if (string.IsNullOrEmpty(needle))
        {
            return 0;
        }

        var count = 0;
        var index = 0;

        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
