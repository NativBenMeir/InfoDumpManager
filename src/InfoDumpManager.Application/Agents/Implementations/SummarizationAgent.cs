using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InfoDumpManager.Application.Services.Caching;
using InfoDumpManager.Application.Services.CostManagement;
using InfoDumpManager.Application.Services.LLM;
using InfoDumpManager.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfoDumpManager.Application.Agents.Implementations;

public sealed class SummarizationAgent : IAgent
{
    private static readonly TimeSpan SummaryCacheTtl = TimeSpan.FromHours(24);

    private readonly ILLMProvider _llmProvider;
    private readonly ILLMRateLimiter _rateLimiter;
    private readonly ITextCache _textCache;
    private readonly ICostManager _costManager;
    private readonly ILogger<SummarizationAgent> _logger;
    private readonly LlmEndpointSettings _chatSettings;

    public SummarizationAgent(
        ILLMProvider llmProvider,
        ILLMRateLimiter rateLimiter,
        ITextCache textCache,
        ICostManager costManager,
        IOptions<AgentLlmSettings> llmSettings,
        ILogger<SummarizationAgent> logger)
    {
        _llmProvider = llmProvider;
        _rateLimiter = rateLimiter;
        _textCache = textCache;
        _costManager = costManager;
        _chatSettings = llmSettings.Value.GetRequiredAgent(Name).Chat;
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

        var promptTemplate = BuildPromptTemplate(options);
        return ExecuteSummarizationAsync(content, promptTemplate, options);
    }

    public async Task<AgentResult> ExecuteAsync(AgentContext context)
    {
        if (string.IsNullOrWhiteSpace(context.ContentText))
        {
            return CreateFailureResult(context, "No content provided for summarization.",
                TimeSpan.Zero, 0, 0m, "no-content");
        }

        var options = new SummarizationOptions();
        var content = context.ContentText;

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

        var promptTemplate = BuildPromptTemplate(options);
        return await ExecuteSummarizationResultAsync(context, content, promptTemplate, options).ConfigureAwait(false);
    }

    private async Task<SummarizationResult> ExecuteSummarizationAsync(
        string content,
        string promptTemplate,
        SummarizationOptions options)
    {
        var promptVariables = new Dictionary<string, string>
        {
            ["content"] = content
        };

        var response = await _llmProvider
            .CallAsync(
                promptTemplate,
                _chatSettings.Provider,
                _chatSettings.Model,
                options.MaxTokens,
                options.Temperature,
                promptVariables: promptVariables)
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
        string promptTemplate,
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
            var promptVariables = new Dictionary<string, string>
            {
                ["content"] = content
            };

            var response = await _rateLimiter.ExecuteAsync(
                    context.TenantId,
                    ct => _llmProvider.CallAsync(
                        promptTemplate,
                        _chatSettings.Provider,
                        _chatSettings.Model,
                        options.MaxTokens,
                        options.Temperature,
                        ct,
                        promptVariables),
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

    private static string BuildPromptTemplate(SummarizationOptions options)
    {
        var lengthInstruction = options.Length switch
        {
            SummaryLength.Short => "1-2 sentences",
            SummaryLength.Medium => "3-5 sentences",
            SummaryLength.Detailed => "1-2 paragraphs",
            _ => "3-5 sentences"
        };

        return $"Summarize the following content in {lengthInstruction}.\n\n" + "{{$content}}";
    }

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
