using System.Diagnostics;
using System.Text.Json;
using InfoDumpManager.Application.Services.CostManagement;
using InfoDumpManager.Application.Services.LLM;
using InfoDumpManager.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfoDumpManager.Application.Agents.Implementations;

public sealed class CategorizationAgent : IAgent
{
    private const string OperationName = "categorization";

    private readonly ILLMProvider _llmProvider;
    private readonly ILLMRateLimiter _rateLimiter;
    private readonly ICostManager _costManager;
    private readonly ILogger<CategorizationAgent> _logger;
    private readonly LlmEndpointSettings _chatSettings;

    public CategorizationAgent(
        ILLMProvider llmProvider,
        ILLMRateLimiter rateLimiter,
        ICostManager costManager,
        IOptions<AgentLlmSettings> llmSettings,
        ILogger<CategorizationAgent> logger)
    {
        _llmProvider = llmProvider;
        _rateLimiter = rateLimiter;
        _costManager = costManager;
        _chatSettings = llmSettings.Value.GetRequiredAgent(Name).Chat;
        _logger = logger;
    }

    public string Name => "CategorizationAgent";

    public AgentCapability Capability => AgentCapability.Categorization;

    public async Task<AgentResult> ExecuteAsync(AgentContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            IReadOnlyCollection<Category> categories;
            if (context.Metadata.CustomData.TryGetValue("categories", out var catObj)
                && catObj is IReadOnlyCollection<Category> loaded)
            {
                categories = loaded;
            }
            else
            {
                categories = Array.Empty<Category>();
            }

            var embeddingBudget = await _costManager
                .CanProcessAsync(context.TenantId, context.Metadata.EstimatedTokenCount, OperationName)
                .ConfigureAwait(false);

            if (!embeddingBudget.Allowed)
            {
                return BuildFailure(context, embeddingBudget.Message, stopwatch.Elapsed);
            }

            var content = context.ContentText;

            var promptTemplate = BuildPromptTemplate(categories);
            var promptVariables = new Dictionary<string, string>
            {
                ["content"] = content
            };

            var response = await _rateLimiter.ExecuteAsync(
                    context.TenantId,
                    ct => _llmProvider.CallAsync(promptTemplate, _chatSettings.Provider, _chatSettings.Model, 220, 0.2f, ct, promptVariables),
                    default)
                .ConfigureAwait(false);

            await _costManager
                .RecordUsageAsync(context.TenantId, context.GEMId, OperationName, response.TokensUsed, response.CostEstimate)
                .ConfigureAwait(false);

            var suggestion = ResolveSuggestion(response.Content, categories);
            stopwatch.Stop();

            _logger.LogInformation(
                "Categorization completed for GEM {GemId}. Tokens {TokensUsed}, Cost {Cost}, DurationMs {DurationMs}, Results {ResultCount}",
                context.GEMId,
                response.TokensUsed,
                response.CostEstimate,
                stopwatch.ElapsedMilliseconds,
                categories.Count);

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
                        { "suggestedCategoryId", suggestion.SuggestedCategoryId?.ToString() ?? string.Empty },
                        { "proposedCategoryName", suggestion.ProposedCategoryName ?? string.Empty },
                        { "confidence", suggestion.ConfidenceScore },
                        { "alternatives", suggestion.AlternativeMatches },
                        { "rationale", suggestion.Rationale ?? string.Empty }
                    }),
                new AgentMetrics(
                    response.TokensUsed,
                    response.CostEstimate,
                    stopwatch.Elapsed,
                    response.RetryCount,
                    response.Provider),
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
        var options = existingCategories.ToList();
        if (options.Count == 0)
        {
            return Task.FromResult(new CategorizationResult(
                null,
                "General",
                "General",
                0.4,
                new List<(Guid, double)>(),
                true,
                "No existing categories available."));
        }

        var selected = options[0];
        return Task.FromResult(new CategorizationResult(
            selected.Id,
            selected.Name,
            selected.Name,
            0.5,
            new List<(Guid, double)>(),
            false,
            "Fallback selection."));
    }

    private static string BuildPromptTemplate(IReadOnlyCollection<Category> categories)
    {
        var categoryList = categories.Count == 0
            ? "(none)"
            : string.Join("\n", categories.Select(c => $"- {c.Id}: {c.Name} | {c.Description}"));

        return $@"Analyze this content and select the best category from the list, or propose a new category name.

Content:
" + "{{$content}}" + $@"

Categories:
{categoryList}

Return JSON with keys:
{{
    ""suggested_category_id"": ""guid-or-null"",
    ""proposed_category_name"": ""name-or-null"",
    ""confidence"": 0.0-1.0,
    ""rationale"": ""short explanation""
}}";
    }

    private static CategorizationResult ResolveSuggestion(string response, IReadOnlyCollection<Category> categories)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return BuildFallback(categories, "Empty LLM response.");
        }

        CategorySuggestionPayload? payload = null;
        try
        {
            payload = JsonSerializer.Deserialize<CategorySuggestionPayload>(response);
        }
        catch
        {
            return BuildFallback(categories, "Invalid LLM response.");
        }

        if (payload is null)
        {
            return BuildFallback(categories, "Invalid LLM response.");
        }

        Guid? suggestedId = null;
        if (!string.IsNullOrWhiteSpace(payload.SuggestedCategoryId)
            && Guid.TryParse(payload.SuggestedCategoryId, out var parsed))
        {
            suggestedId = parsed;
        }

        var suggestedName = payload.ProposedCategoryName;
        if (suggestedId.HasValue)
        {
            var category = categories.FirstOrDefault(c => c.Id == suggestedId);
            if (category is not null)
            {
                suggestedName = category.Name;
            }
        }

        var confidence = payload.Confidence.HasValue
            ? Math.Clamp(payload.Confidence.Value, 0.0, 1.0)
            : suggestedId.HasValue ? 0.75 : 0.55;

        return new CategorizationResult(
            suggestedId,
            suggestedName,
            payload.ProposedCategoryName,
            confidence,
            new List<(Guid, double)>(),
            !suggestedId.HasValue,
            payload.Rationale);
    }

    private static CategorizationResult BuildFallback(IReadOnlyCollection<Category> categories, string rationale)
    {
        var first = categories.FirstOrDefault();
        if (first is null)
        {
            return new CategorizationResult(null, "General", "General", 0.4, new List<(Guid, double)>(), true, rationale);
        }

        return new CategorizationResult(first.Id, first.Name, first.Name, 0.55, new List<(Guid, double)>(), false, rationale);
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
    string? ProposedCategoryName,
    double ConfidenceScore,
    List<(Guid CategoryId, double SimilarityScore)> AlternativeMatches,
    bool ShouldCreateNewCategory,
    string? Rationale);

internal sealed record CategorySuggestionPayload(
    string? SuggestedCategoryId,
    string? ProposedCategoryName,
    double? Confidence,
    string? Rationale);
