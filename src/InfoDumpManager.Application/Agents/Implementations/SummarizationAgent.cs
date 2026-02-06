using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InfoDumpManager.Application.Services.Caching;
using InfoDumpManager.Application.Services.CostManagement;
using InfoDumpManager.Application.Services.LLM;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace InfoDumpManager.Application.Agents.Implementations;

public sealed class SummarizationAgent : IAgent
{
    private static readonly TimeSpan SummaryCacheTtl = TimeSpan.FromHours(24);

    private readonly ILLMProvider _llmProvider;
    private readonly ILLMRateLimiter _rateLimiter;
    private readonly IGEMRepository _gemRepository;
    private readonly ITextCache _textCache;
    private readonly ICostManager _costManager;
    private readonly ILogger<SummarizationAgent> _logger;

    public SummarizationAgent(
        ILLMProvider llmProvider,
        ILLMRateLimiter rateLimiter,
        IGEMRepository gemRepository,
        ITextCache textCache,
        ICostManager costManager,
        ILogger<SummarizationAgent> logger)
    {
        _llmProvider = llmProvider;
        _rateLimiter = rateLimiter;
        _gemRepository = gemRepository;
        _textCache = textCache;
        _costManager = costManager;
        _logger = logger;
    }

    public string Name => "SummarizationAgent";

    public AgentCapability Capability => AgentCapability.Summarization;

    public Task<SummarizationResult> SummarizeAsync(string content, SummarizationOptions options)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Content cannot be empty.", nameof(content));
        }

        var prompt = BuildPrompt(content, options);
        return ExecuteSummarizationAsync(prompt, options);
    }

    public async Task<AgentResult> ExecuteAsync(AgentContext context)
    {
        var gem = await _gemRepository.GetByIdAsync(context.GEMId).ConfigureAwait(false);
        if (gem is null || gem.TenantId != context.TenantId)
        {
            return CreateFailureResult(context, "GEM not found for tenant.", TimeSpan.Zero, 0, 0m, "missing");
        }

        var options = new SummarizationOptions();
        var content = string.IsNullOrWhiteSpace(context.ContentText)
            ? BuildContentFromGem(gem)
            : context.ContentText;

        var cacheKey = BuildCacheKey(context.TenantId, content);
        var cached = await _textCache.TryGetAsync(cacheKey).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            CachedSummary? cachedSummary = null;
            try
            {
                cachedSummary = JsonSerializer.Deserialize<CachedSummary>(cached);
            }
            catch
            {
                cachedSummary = null;
            }

            if (cachedSummary is not null && !string.IsNullOrWhiteSpace(cachedSummary.Text))
            {
                var summary = GEMSummary.Create(
                    cachedSummary.Text,
                    cachedSummary.Model,
                    cachedSummary.Tokens,
                    cachedSummary.GeneratedAt);

                return new AgentResult(
                    true,
                    "Summary completed (cache)",
                    new AgentResultData(
                        Name,
                        DateTimeOffset.UtcNow,
                        new Dictionary<string, object>
                        {
                            { "summary", summary.Text },
                            { "summaryObject", summary },
                            { "model", summary.Model },
                            { "tokenCount", summary.TokenCount },
                            { "cacheHit", true }
                        }),
                    new AgentMetrics(summary.TokenCount, 0m, TimeSpan.Zero, 0, "cache"));
            }
        }

        var prompt = BuildPrompt(content, options);
        return await ExecuteSummarizationResultAsync(context, content, prompt, options).ConfigureAwait(false);
    }

    private async Task<SummarizationResult> ExecuteSummarizationAsync(string prompt, SummarizationOptions options)
    {
        var response = await _llmProvider
            .CallAsync(prompt, options.Model, options.MaxTokens, options.Temperature)
            .ConfigureAwait(false);

        return new SummarizationResult(
            response.Content,
            response.TokensUsed,
            response.Model,
            DateTimeOffset.UtcNow);
    }

    private async Task<AgentResult> ExecuteSummarizationResultAsync(
        AgentContext context,
        string content,
        string prompt,
        SummarizationOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        var estimatedTokens = context.Metadata.EstimatedTokenCount;
        var budgetCheck = await _costManager
            .CanProcessAsync(context.TenantId, estimatedTokens, "summarization")
            .ConfigureAwait(false);

        if (!budgetCheck.Allowed)
        {
            return CreateFailureResult(context, budgetCheck.Message, stopwatch.Elapsed, 0, 0m, "budget");
        }

        try
        {
            var response = await _rateLimiter.ExecuteAsync(
                    context.TenantId,
                    ct => _llmProvider.CallAsync(prompt, options.Model, options.MaxTokens, options.Temperature, ct),
                    default)
                .ConfigureAwait(false);

            var resolvedSummary = string.IsNullOrWhiteSpace(response.Content)
                ? BuildDeterministicSummary(content)
                : response.Content.Trim();

            var summaryObject = GEMSummary.Create(
                resolvedSummary,
                response.Model,
                response.TokensUsed,
                DateTimeOffset.UtcNow);

            await _costManager.RecordUsageAsync(
                context.TenantId,
                context.GEMId,
                "summarization",
                response.TokensUsed,
                response.CostEstimate)
                .ConfigureAwait(false);

            stopwatch.Stop();

            _logger.LogInformation(
                "Summarization completed for GEM {GemId}. Tokens {TokensUsed}, Cost {Cost}, DurationMs {DurationMs}, Retries {RetryCount}",
                context.GEMId,
                response.TokensUsed,
                response.CostEstimate,
                stopwatch.ElapsedMilliseconds,
                response.RetryCount);

            var cacheKey = BuildCacheKey(context.TenantId, content);
            var cachedSummary = new CachedSummary(
                summaryObject.Text,
                summaryObject.Model,
                summaryObject.TokenCount,
                summaryObject.GeneratedAt);
            await _textCache.SetAsync(cacheKey, JsonSerializer.Serialize(cachedSummary), SummaryCacheTtl)
                .ConfigureAwait(false);

            return new AgentResult(
                true,
                "Summary completed",
                new AgentResultData(
                    Name,
                    DateTimeOffset.UtcNow,
                    new Dictionary<string, object>
                    {
                        { "summary", summaryObject.Text },
                        { "summaryObject", summaryObject },
                        { "model", summaryObject.Model },
                        { "tokenCount", summaryObject.TokenCount },
                        { "cacheHit", false }
                    }),
                new AgentMetrics(
                    response.TokensUsed,
                    response.CostEstimate,
                    stopwatch.Elapsed,
                    response.RetryCount,
                    response.Provider));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Summarization failed for GEM {GemId}", context.GEMId);
            return CreateFailureResult(context, ex.Message, stopwatch.Elapsed, 0, 0m, "error");
        }
    }

    private static string BuildPrompt(string content, SummarizationOptions options)
    {
        var lengthInstruction = options.Length switch
        {
            SummaryLength.Short => "1-2 sentences",
            SummaryLength.Medium => "3-5 sentences",
            SummaryLength.Detailed => "1-2 paragraphs",
            _ => "3-5 sentences"
        };

        return $"Summarize the following content in {lengthInstruction}.\n\n{content}";
    }

    private static string BuildContentFromGem(InfoDumpManager.Domain.Entities.GEM gem)
        => $"Title: {gem.Title}\n\n{gem.Snapshot.HtmlContent}";

    private static string BuildDeterministicSummary(string content)
    {
        var words = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return "Summary unavailable.";
        }

        var limit = Math.Min(40, words.Length);
        return string.Join(' ', words.Take(limit)) + (words.Length > limit ? "..." : string.Empty);
    }

    private static string BuildCacheKey(Guid tenantId, string content)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(content));
        var hash = Convert.ToHexString(bytes);
        return $"summary:{tenantId}:{hash}";
    }

    private sealed record CachedSummary(
        string Text,
        string Model,
        int Tokens,
        DateTimeOffset GeneratedAt);

    private AgentResult CreateFailureResult(
        AgentContext context,
        string message,
        TimeSpan duration,
        int tokens,
        decimal cost,
        string reason)
    {
        var failureMessage = reason == "budget"
            ? "Summarization failed: budget limit"
            : "Summarization failed";

        return new AgentResult(
            false,
            failureMessage,
            new AgentResultData(
                Name,
                DateTimeOffset.UtcNow,
                new Dictionary<string, object>
                {
                    { "reason", reason },
                    { "message", message }
                }),
            new AgentMetrics(tokens, cost, duration, 0, "unknown"),
            new List<string> { message },
            new AgentResultConfidence(0.2, true, "Summarization failed"));
    }
}

public enum SummaryLength
{
    Short,
    Medium,
    Detailed
}

public sealed record SummarizationOptions(
    SummaryLength Length = SummaryLength.Medium,
    string Language = "en",
    string Model = "gpt-4",
    int MaxTokens = 300,
    float Temperature = 0.3f);

public sealed record SummarizationResult(
    string Summary,
    int TokensUsed,
    string ModelUsed,
    DateTimeOffset GeneratedAt);
