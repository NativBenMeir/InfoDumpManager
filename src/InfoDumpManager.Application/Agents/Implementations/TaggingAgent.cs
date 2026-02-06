using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using InfoDumpManager.Application.Services.Caching;
using InfoDumpManager.Application.Services.CostManagement;
using InfoDumpManager.Application.Services.Embeddings;
using InfoDumpManager.Application.Services.LLM;
using InfoDumpManager.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace InfoDumpManager.Application.Agents.Implementations;

public sealed class TaggingAgent : IAgent
{
    private const string OperationName = "tagging";
    private static readonly TimeSpan TagCacheTtl = TimeSpan.FromHours(24);
    private const int CandidateTagLimit = 20;
    private const int MaxSuggestionCount = 5;
    private const double AcceptSimilarityThreshold = 0.7;
    private const double MergeSimilarityThreshold = 0.85;
    private const double MergeShortTagThreshold = 0.92;
    private const int ShortTagLength = 4;

    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IVectorStore _vectorStore;
    private readonly IEmbeddingCache _embeddingCache;
    private readonly ITextCache _textCache;
    private readonly ITagRepository _tagRepository;
    private readonly ILLMProvider _llmProvider;
    private readonly ILLMRateLimiter _rateLimiter;
    private readonly ICostManager _costManager;
    private readonly ILogger<TaggingAgent> _logger;

    public TaggingAgent(
        IEmbeddingProvider embeddingProvider,
        IVectorStore vectorStore,
        IEmbeddingCache embeddingCache,
        ITextCache textCache,
        ITagRepository tagRepository,
        ILLMProvider llmProvider,
        ILLMRateLimiter rateLimiter,
        ICostManager costManager,
        ILogger<TaggingAgent> logger)
    {
        _embeddingProvider = embeddingProvider;
        _vectorStore = vectorStore;
        _embeddingCache = embeddingCache;
        _textCache = textCache;
        _tagRepository = tagRepository;
        _llmProvider = llmProvider;
        _rateLimiter = rateLimiter;
        _costManager = costManager;
        _logger = logger;
    }

    public string Name => "TaggingAgent";

    public AgentCapability Capability => AgentCapability.Tagging;

    public async Task<AgentResult> ExecuteAsync(AgentContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (context.ContentText.Length <= 10)
            {
                return new AgentResult(
                    true,
                    "Tagging skipped: insufficient content",
                    new AgentResultData(
                        Name,
                        DateTimeOffset.UtcNow,
                        new Dictionary<string, object>
                        {
                            { "tags", new List<string>() },
                            { "suggestedTags", new List<TagSuggestionResult>() }
                        }),
                    new AgentMetrics(0, 0m, stopwatch.Elapsed, 0, "none"),
                    null,
                    new AgentResultConfidence(0.3, true, "Content too short for tagging."));
            }

            var budgetCheck = await _costManager
                .CanProcessAsync(context.TenantId, context.Metadata.EstimatedTokenCount, OperationName)
                .ConfigureAwait(false);

            if (!budgetCheck.Allowed)
            {
                return BuildFailure(context, budgetCheck.Message, stopwatch.Elapsed);
            }

            await EnsureTagEmbeddingsAsync(context).ConfigureAwait(false);

            var cacheKey = BuildCacheKey("tag-suggestions", context.TenantId, context.ContentText);
            var cached = await _textCache.TryGetAsync(cacheKey).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(cached))
            {
                List<TagSuggestionResult>? cachedTags = null;
                try
                {
                    cachedTags = JsonSerializer.Deserialize<List<TagSuggestionResult>>(cached);
                }
                catch
                {
                    cachedTags = null;
                }

                if (cachedTags is not null)
                {
                    var tagNames = cachedTags.Select(t => t.TagName).ToList();
                    return new AgentResult(
                        true,
                        "Tagging completed (cache)",
                        new AgentResultData(
                            Name,
                            DateTimeOffset.UtcNow,
                            new Dictionary<string, object>
                            {
                                { "tags", tagNames },
                                { "suggestedTags", cachedTags },
                                { "cacheHit", true }
                            }),
                        new AgentMetrics(0, 0m, stopwatch.Elapsed, 0, "cache"));
                }
            }

            var embedding = await GetOrCreateEmbeddingAsync(context).ConfigureAwait(false);

            if (embedding.TokensUsed > 0)
            {
                await _costManager.RecordUsageAsync(
                    context.TenantId,
                    context.GEMId,
                    OperationName,
                    embedding.TokensUsed,
                    embedding.CostEstimate)
                    .ConfigureAwait(false);
            }

            var totalTokens = embedding.TokensUsed;
            var totalCost = embedding.CostEstimate;
            var providerUsed = embedding.Provider;
            var retryCount = 0;

            var searchResults = await SuggestTagsAsync(context.TenantId, embedding.Vector).ConfigureAwait(false);
            var suggestions = searchResults.Suggestions;
            if (suggestions.Count == 0)
            {
                var llmResult = await SuggestTagsWithLlmAsync(context, searchResults.Candidates).ConfigureAwait(false);
                suggestions = await MergeLlmSuggestionsAsync(context, llmResult, searchResults.Candidates)
                    .ConfigureAwait(false);

                await _costManager.RecordUsageAsync(
                    context.TenantId,
                    context.GEMId,
                    OperationName,
                    llmResult.Response.TokensUsed,
                    llmResult.Response.CostEstimate)
                    .ConfigureAwait(false);

                totalTokens += llmResult.Response.TokensUsed;
                totalCost += llmResult.Response.CostEstimate;
                providerUsed = llmResult.Response.Provider;
                retryCount = llmResult.Response.RetryCount;
            }

            if (suggestions.Count == 0)
            {
                suggestions = BuildDeterministicTags(context.ContentText);
            }

            await _textCache.SetAsync(cacheKey, JsonSerializer.Serialize(suggestions), TagCacheTtl)
                .ConfigureAwait(false);

            stopwatch.Stop();

            _logger.LogInformation(
                "Tagging completed for GEM {GemId}. Tokens {TokensUsed}, Cost {Cost}, DurationMs {DurationMs}, Tags {TagCount}",
                context.GEMId,
                totalTokens,
                totalCost,
                stopwatch.ElapsedMilliseconds,
                suggestions.Count);

            return new AgentResult(
                true,
                "Tagging completed",
                new AgentResultData(
                    Name,
                    DateTimeOffset.UtcNow,
                    new Dictionary<string, object>
                    {
                        { "tags", suggestions.Select(t => t.TagName).ToList() },
                        { "suggestedTags", suggestions },
                        { "cacheHit", false }
                    }),
                new AgentMetrics(
                    totalTokens,
                    totalCost,
                    stopwatch.Elapsed,
                    retryCount,
                    providerUsed));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tagging failed for GEM {GemId}", context.GEMId);
            return BuildFailure(context, ex.Message, stopwatch.Elapsed);
        }
    }

    public Task<TaggingResult> TagAsync(string content)
    {
        var tags = BuildDeterministicTags(content);
        return Task.FromResult(new TaggingResult(tags.Select(t => t.TagName).ToList(), DateTimeOffset.UtcNow));
    }

    private async Task<EmbeddingResponse> GetOrCreateEmbeddingAsync(AgentContext context)
    {
        var embeddingKey = BuildCacheKey("embedding", context.TenantId, context.ContentText);
        var cached = await _embeddingCache.TryGetAsync(embeddingKey).ConfigureAwait(false);
        if (cached is not null)
        {
            return new EmbeddingResponse(cached, "cache", "cache", 0, 0m);
        }

        try
        {
            var embedding = await _embeddingProvider
                .GenerateEmbeddingAsync(context.ContentText, "text-embedding-3-large")
                .ConfigureAwait(false);

            await _embeddingCache.SetAsync(embeddingKey, embedding.Vector, TagCacheTtl).ConfigureAwait(false);
            return embedding;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Embedding generation failed for GEM {GemId}", context.GEMId);
            return new EmbeddingResponse(Array.Empty<float>(), "fallback", "fallback", 0, 0m);
        }
    }

    private async Task<TagSearchResults> SuggestTagsAsync(Guid tenantId, float[] vector)
    {
        if (vector.Length == 0)
        {
            return new TagSearchResults(
                new List<TagSuggestionResult>(),
                new List<TagSuggestionResult>());
        }

        var results = await _vectorStore
            .SearchSimilarAsync(new EmbeddingSearchRequest(tenantId, "tag", vector, CandidateTagLimit))
            .ConfigureAwait(false);

        if (results.Count == 0)
        {
            return new TagSearchResults(
                new List<TagSuggestionResult>(),
                new List<TagSuggestionResult>());
        }

        var tagIds = results.Select(r => r.SourceId).Distinct().ToList();
        var tags = await _tagRepository.ListByIdsAsync(tenantId, tagIds).ConfigureAwait(false);
        if (tags.Count == 0)
        {
            return new TagSearchResults(
                new List<TagSuggestionResult>(),
                new List<TagSuggestionResult>());
        }

        var tagMap = tags.ToDictionary(t => t.Id, t => t.Name);
        var suggestions = new List<TagSuggestionResult>();
        var candidates = new List<TagSuggestionResult>();
        foreach (var result in results)
        {
            if (!tagMap.TryGetValue(result.SourceId, out var name))
            {
                continue;
            }

            var score = 1.0 / (1.0 + result.Distance);
            candidates.Add(new TagSuggestionResult(result.SourceId, name, score));
            if (score >= AcceptSimilarityThreshold)
            {
                suggestions.Add(new TagSuggestionResult(result.SourceId, name, score));
            }
        }

        var resolvedCandidates = candidates
            .GroupBy(x => x.TagId)
            .Select(g => g.OrderByDescending(x => x.SimilarityScore).First())
            .OrderByDescending(x => x.SimilarityScore)
            .ToList();

        var resolvedSuggestions = suggestions
            .GroupBy(x => x.TagId)
            .Select(g => g.OrderByDescending(x => x.SimilarityScore).First())
            .OrderByDescending(x => x.SimilarityScore)
            .Take(MaxSuggestionCount)
            .ToList();
        return new TagSearchResults(resolvedSuggestions, resolvedCandidates);
    }

    private async Task<LlmTagSuggestionResult> SuggestTagsWithLlmAsync(
        AgentContext context,
        IReadOnlyList<TagSuggestionResult> candidates)
    {
        try
        {
            var prompt = BuildTagPrompt(context.ContentText, candidates);
            var response = await _rateLimiter.ExecuteAsync(
                    context.TenantId,
                    ct => _llmProvider.CallAsync(prompt, "gpt-4", 120, 0.2f, ct),
                    default)
                .ConfigureAwait(false);

            var payload = TryParseTagSelection(response.Content);
            var selectedExisting = payload?.SelectedExisting ?? new List<string>();
            var proposedNew = payload?.ProposedNew ?? new List<string>();

            return new LlmTagSuggestionResult(
                selectedExisting,
                proposedNew,
                response);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM tag suggestion failed for GEM {GemId}", context.GEMId);
            return new LlmTagSuggestionResult(
                new List<string>(),
                new List<string>(),
                new LLMResponse(string.Empty, "fallback", "fallback", 0, 0m, "error", 0));
        }
    }

    private async Task<List<TagSuggestionResult>> MergeLlmSuggestionsAsync(
        AgentContext context,
        LlmTagSuggestionResult llmResult,
        IReadOnlyList<TagSuggestionResult> candidates)
    {
        var selectedExisting = ResolveSelectedExisting(candidates, llmResult.SelectedExisting);
        var proposedNew = NormalizeProposedNew(llmResult.ProposedNew, selectedExisting);
        var mergedNew = await MergeProposedTagsAsync(context, proposedNew).ConfigureAwait(false);

        return DeduplicateSuggestions(selectedExisting.Concat(mergedNew))
            .OrderByDescending(x => x.SimilarityScore)
            .Take(MaxSuggestionCount)
            .ToList();
    }

    private static List<TagSuggestionResult> ResolveSelectedExisting(
        IReadOnlyList<TagSuggestionResult> candidates,
        IReadOnlyCollection<string> selections)
    {
        if (candidates.Count == 0 || selections.Count == 0)
        {
            return new List<TagSuggestionResult>();
        }

        var byId = candidates.ToDictionary(x => x.TagId, x => x);
        var byName = candidates.ToDictionary(x => x.TagName, StringComparer.OrdinalIgnoreCase);
        var resolved = new List<TagSuggestionResult>();

        foreach (var selection in selections)
        {
            if (string.IsNullOrWhiteSpace(selection))
            {
                continue;
            }

            var trimmed = selection.Trim();
            if (Guid.TryParse(trimmed, out var id) && byId.TryGetValue(id, out var matchById))
            {
                resolved.Add(matchById);
                continue;
            }

            if (byName.TryGetValue(trimmed, out var matchByName))
            {
                resolved.Add(matchByName);
            }
        }

        return resolved;
    }

    private static List<string> NormalizeProposedNew(
        IReadOnlyCollection<string> proposedNew,
        IReadOnlyCollection<TagSuggestionResult> selectedExisting)
    {
        if (proposedNew.Count == 0)
        {
            return new List<string>();
        }

        var existingNames = new HashSet<string>(
            selectedExisting.Select(x => x.TagName),
            StringComparer.OrdinalIgnoreCase);

        return proposedNew
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Where(tag => !existingNames.Contains(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<List<TagSuggestionResult>> MergeProposedTagsAsync(
        AgentContext context,
        IReadOnlyCollection<string> proposedNew)
    {
        if (proposedNew.Count == 0)
        {
            return new List<TagSuggestionResult>();
        }

        var merged = new List<TagSuggestionResult>();
        foreach (var tag in proposedNew)
        {
            var normalized = tag.ToLowerInvariant();
            var embedding = await GetOrCreateTagEmbeddingAsync(context, normalized).ConfigureAwait(false);
            if (embedding.TokensUsed > 0)
            {
                await _costManager.RecordUsageAsync(
                    context.TenantId,
                    context.GEMId,
                    OperationName,
                    embedding.TokensUsed,
                    embedding.CostEstimate)
                    .ConfigureAwait(false);
            }

            var threshold = normalized.Length <= ShortTagLength
                ? MergeShortTagThreshold
                : MergeSimilarityThreshold;

            var match = await ResolveExistingTagMatchAsync(context.TenantId, embedding.Vector, threshold)
                .ConfigureAwait(false);
            if (match is not null)
            {
                merged.Add(match);
                continue;
            }

            merged.Add(new TagSuggestionResult(Guid.Empty, normalized, 0.55));
        }

        return merged;
    }

    private async Task<TagSuggestionResult?> ResolveExistingTagMatchAsync(
        Guid tenantId,
        float[] vector,
        double threshold)
    {
        if (vector.Length == 0)
        {
            return null;
        }

        var results = await _vectorStore
            .SearchSimilarAsync(new EmbeddingSearchRequest(tenantId, "tag", vector, 1))
            .ConfigureAwait(false);

        if (results.Count == 0)
        {
            return null;
        }

        var best = results[0];
        var score = 1.0 / (1.0 + best.Distance);
        if (score < threshold)
        {
            return null;
        }

        var tags = await _tagRepository
            .ListByIdsAsync(tenantId, new[] { best.SourceId })
            .ConfigureAwait(false);

        var tag = tags.FirstOrDefault();
        if (tag is null)
        {
            return null;
        }

        return new TagSuggestionResult(tag.Id, tag.Name, score);
    }

    private async Task<EmbeddingResponse> GetOrCreateTagEmbeddingAsync(AgentContext context, string tag)
    {
        var embeddingKey = BuildCacheKey("tag-proposal-embedding", context.TenantId, tag);
        var cached = await _embeddingCache.TryGetAsync(embeddingKey).ConfigureAwait(false);
        if (cached is not null)
        {
            return new EmbeddingResponse(cached, "cache", "cache", 0, 0m);
        }

        try
        {
            var embedding = await _embeddingProvider
                .GenerateEmbeddingAsync(tag, "text-embedding-3-large")
                .ConfigureAwait(false);

            await _embeddingCache.SetAsync(embeddingKey, embedding.Vector, TagCacheTtl).ConfigureAwait(false);
            return embedding;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Embedding generation failed for proposed tag {Tag}", tag);
            return new EmbeddingResponse(Array.Empty<float>(), "fallback", "fallback", 0, 0m);
        }
    }

    private static IEnumerable<TagSuggestionResult> DeduplicateSuggestions(
        IEnumerable<TagSuggestionResult> suggestions)
    {
        return suggestions
            .GroupBy(s => s.TagId == Guid.Empty ? s.TagName : s.TagId.ToString(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.SimilarityScore).First());
    }

    private static LlmTagSelectionPayload? TryParseTagSelection(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var payload = ExtractJsonPayload(content);
        if (payload.StartsWith("[", StringComparison.Ordinal))
        {
            try
            {
                var tags = JsonSerializer.Deserialize<List<string>>(payload);
                return new LlmTagSelectionPayload(new List<string>(), tags ?? new List<string>());
            }
            catch
            {
                return null;
            }
        }

        try
        {
            return JsonSerializer.Deserialize<LlmTagSelectionPayload>(
                payload,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }

    private static string ExtractJsonPayload(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstLineEnd = trimmed.IndexOf('\n');
        if (firstLineEnd >= 0)
        {
            trimmed = trimmed[(firstLineEnd + 1)..];
        }

        var fenceIndex = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        if (fenceIndex >= 0)
        {
            trimmed = trimmed[..fenceIndex];
        }

        return trimmed.Trim();
    }

    private static string BuildTagPrompt(string content, IReadOnlyList<TagSuggestionResult> candidates)
    {
        var candidateBlock = candidates.Count == 0
            ? "(none)"
            : string.Join("\n", candidates.Select(c => $"- {c.TagId}: {c.TagName} (score {c.SimilarityScore:F2})"));

        return $@"Select the most relevant tags for the content below.

Use existing tags if they apply. Only propose new tags if none of the existing tags fit.

Content:
{content}

Existing tag candidates:
{candidateBlock}

Return JSON only with keys:
{{
    ""selected_existing"": [""tag-id-or-name""],
    ""proposed_new"": [""new-tag""]
}}";
    }

    private async Task EnsureTagEmbeddingsAsync(AgentContext context)
    {
        var tags = await _tagRepository.ListByTenantAsync(context.TenantId).ConfigureAwait(false);
        if (tags.Count == 0)
        {
            return;
        }

        foreach (var tag in tags)
        {
            var cacheKey = BuildCacheKey("tag-embedding", context.TenantId, tag.Id.ToString());
            var cached = await _embeddingCache.TryGetAsync(cacheKey).ConfigureAwait(false);
            if (cached is not null)
            {
                continue;
            }

            try
            {
                var embedding = await _embeddingProvider
                    .GenerateEmbeddingAsync(tag.Name, "text-embedding-3-large")
                    .ConfigureAwait(false);

                if (embedding.Vector.Length == 0)
                {
                    continue;
                }

                await _vectorStore.StoreAsync(new EmbeddingRecord(
                    Guid.NewGuid(),
                    context.TenantId,
                    tag.Id,
                    "tag",
                    embedding.Model,
                    embedding.Vector,
                    tag.Name,
                    DateTimeOffset.UtcNow)).ConfigureAwait(false);

                await _embeddingCache.SetAsync(cacheKey, embedding.Vector, TagCacheTtl).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate embedding for tag {TagId}", tag.Id);
            }
        }
    }

    private static List<TagSuggestionResult> BuildDeterministicTags(string content)
    {
        var words = content
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim().Trim(',', '.', ';', ':', '!', '?', '"', '\'', '(', ')'))
            .Where(w => w.Length > 3)
            .GroupBy(w => w, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => new TagSuggestionResult(Guid.Empty, g.Key.ToLowerInvariant(), 0.4))
            .ToList();

        return words;
    }

    private static string BuildCacheKey(string prefix, Guid tenantId, string content)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(content));
        var hash = Convert.ToHexString(bytes);
        return $"{prefix}:{tenantId}:{hash}";
    }

    private AgentResult BuildFailure(AgentContext context, string message, TimeSpan duration)
    {
        return new AgentResult(
            false,
            "Tagging failed",
            new AgentResultData(
                Name,
                DateTimeOffset.UtcNow,
                new Dictionary<string, object>
                {
                    { "message", message }
                }),
            new AgentMetrics(0, 0m, duration, 0, "unknown"),
            new List<string> { message },
            new AgentResultConfidence(0.2, true, "Tagging failed"));
    }

    private sealed record LlmTagSuggestionResult(
        List<string> SelectedExisting,
        List<string> ProposedNew,
        LLMResponse Response);

    private sealed record LlmTagSelectionPayload(
        [property: JsonPropertyName("selected_existing")] List<string>? SelectedExisting,
        [property: JsonPropertyName("proposed_new")] List<string>? ProposedNew);

    private sealed record TagSearchResults(
        List<TagSuggestionResult> Suggestions,
        List<TagSuggestionResult> Candidates);
}

public sealed record TaggingResult(
    IReadOnlyList<string> Tags,
    DateTimeOffset GeneratedAt);
