using System.Diagnostics;
using InfoDumpManager.Application.Services.CostManagement;
using InfoDumpManager.Application.Services.Embeddings;
using InfoDumpManager.Application.Services.LLM;
using Microsoft.Extensions.Logging;

namespace InfoDumpManager.Application.Agents.Implementations;

public sealed class CategorizationAgent : IAgent
{
    private const string OperationName = "categorization";

    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IVectorStore _vectorStore;
    private readonly ILLMProvider _llmProvider;
    private readonly ICostManager _costManager;
    private readonly ILogger<CategorizationAgent> _logger;

    public CategorizationAgent(
        IEmbeddingProvider embeddingProvider,
        IVectorStore vectorStore,
        ILLMProvider llmProvider,
        ICostManager costManager,
        ILogger<CategorizationAgent> logger)
    {
        _embeddingProvider = embeddingProvider;
        _vectorStore = vectorStore;
        _llmProvider = llmProvider;
        _costManager = costManager;
        _logger = logger;
    }

    public string Name => "CategorizationAgent";

    public AgentCapability Capability => AgentCapability.Categorization;

    public async Task<AgentResult> ExecuteAsync(AgentContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var embeddingBudget = await _costManager
                .CanProcessAsync(context.TenantId, context.Metadata.EstimatedTokenCount, OperationName)
                .ConfigureAwait(false);

            if (!embeddingBudget.Allowed)
            {
                return BuildFailure(context, embeddingBudget.Message, stopwatch.Elapsed);
            }

            var embedding = await _embeddingProvider
                .GenerateEmbeddingAsync(context.ContentText, "text-embedding-3-large")
                .ConfigureAwait(false);

            await _costManager
                .RecordUsageAsync(context.TenantId, context.GEMId, OperationName, embedding.TokensUsed, embedding.CostEstimate)
                .ConfigureAwait(false);

            var searchResults = await _vectorStore
                .SearchSimilarAsync(new EmbeddingSearchRequest(
                    context.TenantId,
                    "category",
                    embedding.Vector,
                    3))
                .ConfigureAwait(false);

            var suggestion = await SuggestCategoryAsync(context, searchResults).ConfigureAwait(false);
            stopwatch.Stop();

            _logger.LogInformation(
                "Categorization completed for GEM {GemId}. Tokens {TokensUsed}, Cost {Cost}, DurationMs {DurationMs}, Results {ResultCount}",
                context.GEMId,
                embedding.TokensUsed,
                embedding.CostEstimate,
                stopwatch.ElapsedMilliseconds,
                searchResults.Count);

            var requiresManualReview = suggestion.ShouldCreateNewCategory || suggestion.ConfidenceScore < 0.6;

            return new AgentResult(
                true,
                "Categorization completed",
                new AgentResultData(
                    Name,
                    DateTimeOffset.UtcNow,
                    new Dictionary<string, object>
                    {
                        { "category", suggestion.SuggestedCategoryName ?? string.Empty },
                        { "suggestedCategory", suggestion.SuggestedCategoryName ?? string.Empty },
                        { "confidence", suggestion.ConfidenceScore },
                        { "alternatives", suggestion.AlternativeMatches }
                    }),
                new AgentMetrics(
                    embedding.TokensUsed,
                    embedding.CostEstimate,
                    stopwatch.Elapsed,
                    0,
                    embedding.Provider),
                null,
                new AgentResultConfidence(
                    suggestion.ConfidenceScore,
                    requiresManualReview,
                    requiresManualReview ? "Low confidence categorization" : "High confidence categorization"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Categorization failed for GEM {GemId}", context.GEMId);
            return BuildFailure(context, ex.Message, stopwatch.Elapsed);
        }
    }

    public Task<CategorizationResult> CategorizeAsync(
        string content,
        IEnumerable<CategoryOption> existingCategories)
    {
        var options = new Dictionary<Guid, CategoryOption>(existingCategories.ToDictionary(x => x.Id));
        return Task.FromResult(new CategorizationResult(
            null,
            null,
            0.0,
            options.Select(o => (o.Key, 0.0)).ToList(),
            true));
    }

    private async Task<CategorizationResult> SuggestCategoryAsync(
        AgentContext context,
        IReadOnlyList<EmbeddingSearchResult> searchResults)
    {
        if (searchResults.Count == 0)
        {
            var prompt = $"Suggest a category name for the following content:\n\n{context.ContentText}";
            var budgetCheck = await _costManager
                .CanProcessAsync(context.TenantId, context.Metadata.EstimatedTokenCount, OperationName)
                .ConfigureAwait(false);

            if (!budgetCheck.Allowed)
            {
                return new CategorizationResult(null, null, 0.0, new List<(Guid, double)>(), true);
            }

            var response = await _llmProvider
                .CallAsync(prompt, "gpt-4", 60, 0.2f)
                .ConfigureAwait(false);

            if (response is null)
            {
                return new CategorizationResult(null, null, 0.0, new List<(Guid, double)>(), true);
            }

            await _costManager.RecordUsageAsync(
                context.TenantId,
                context.GEMId,
                OperationName,
                response.TokensUsed,
                response.CostEstimate)
                .ConfigureAwait(false);

            return new CategorizationResult(
                null,
                response.Content.Trim(),
                0.5,
                new List<(Guid, double)>(),
                true);
        }

        var best = searchResults.First();
        var alternatives = searchResults.Skip(1).Select(r => (r.SourceId, r.Distance)).ToList();
        return new CategorizationResult(best.SourceId, best.Metadata, 1.0 / (1.0 + best.Distance), alternatives, false);
    }

    private AgentResult BuildFailure(AgentContext context, string message, TimeSpan duration)
    {
        return new AgentResult(
            false,
            "Categorization failed",
            new AgentResultData(
                Name,
                DateTimeOffset.UtcNow,
                new Dictionary<string, object>
                {
                    { "message", message }
                }),
            new AgentMetrics(0, 0m, duration, 0, "unknown"),
            new List<string> { message },
            new AgentResultConfidence(0.2, true, "Categorization failed"));
    }
}

public sealed record CategoryOption(
    Guid Id,
    string Name,
    string? Description,
    int GEMCount);

public sealed record CategorizationResult(
    Guid? SuggestedCategoryId,
    string? SuggestedCategoryName,
    double ConfidenceScore,
    List<(Guid CategoryId, double SimilarityScore)> AlternativeMatches,
    bool ShouldCreateNewCategory);
