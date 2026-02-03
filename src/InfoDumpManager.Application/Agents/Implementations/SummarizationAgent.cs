using System.Diagnostics;
using InfoDumpManager.Application.Services.CostManagement;
using InfoDumpManager.Application.Services.LLM;
using Microsoft.Extensions.Logging;

namespace InfoDumpManager.Application.Agents.Implementations;

public sealed class SummarizationAgent : IAgent
{
    private readonly ILLMProvider _llmProvider;
    private readonly ICostManager _costManager;
    private readonly ILogger<SummarizationAgent> _logger;

    public SummarizationAgent(
        ILLMProvider llmProvider,
        ICostManager costManager,
        ILogger<SummarizationAgent> logger)
    {
        _llmProvider = llmProvider;
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
        var options = new SummarizationOptions();
        var prompt = BuildPrompt(context.ContentText, options);
        return await ExecuteSummarizationResultAsync(context, prompt, options).ConfigureAwait(false);
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
            var response = await _llmProvider
                .CallAsync(prompt, options.Model, options.MaxTokens, options.Temperature)
                .ConfigureAwait(false);

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

            return new AgentResult(
                true,
                "Summary completed",
                new AgentResultData(
                    Name,
                    DateTimeOffset.UtcNow,
                    new Dictionary<string, object>
                    {
                        { "summary", response.Content },
                        { "model", response.Model },
                        { "tokenCount", response.TokensUsed }
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
