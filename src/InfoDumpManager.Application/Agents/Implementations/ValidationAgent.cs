using System.Diagnostics;
using InfoDumpManager.Application.Services.CostManagement;
using InfoDumpManager.Application.Services.LLM;
using Microsoft.Extensions.Logging;

namespace InfoDumpManager.Application.Agents.Implementations;

public sealed class ValidationAgent : IAgent
{
    private const string OperationName = "validation";

    private readonly ILLMProvider _llmProvider;
    private readonly ILLMRateLimiter _rateLimiter;
    private readonly ICostManager _costManager;
    private readonly ILogger<ValidationAgent> _logger;

    public ValidationAgent(
        ILLMProvider llmProvider,
        ILLMRateLimiter rateLimiter,
        ICostManager costManager,
        ILogger<ValidationAgent> logger)
    {
        _llmProvider = llmProvider;
        _rateLimiter = rateLimiter;
        _costManager = costManager;
        _logger = logger;
    }

    public string Name => "ValidationAgent";

    public AgentCapability Capability => AgentCapability.Validation;

    public async Task<AgentResult> ExecuteAsync(AgentContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var budgetCheck = await _costManager
                .CanProcessAsync(context.TenantId, context.Metadata.EstimatedTokenCount, OperationName)
                .ConfigureAwait(false);

            if (!budgetCheck.Allowed)
            {
                return BuildFailure(context, budgetCheck.Message, stopwatch.Elapsed);
            }

            var prompt = $"Validate the quality and clarity of the following content. Return 'OK' if acceptable, or describe issues.\n\n{context.ContentText}";
            var response = await _rateLimiter.ExecuteAsync(
                    context.TenantId,
                    ct => _llmProvider.CallAsync(prompt, "gpt-4", 80, 0.2f, ct),
                    default)
                .ConfigureAwait(false);

            await _costManager.RecordUsageAsync(
                context.TenantId,
                context.GEMId,
                OperationName,
                response.TokensUsed,
                response.CostEstimate)
                .ConfigureAwait(false);

            stopwatch.Stop();

            var trimmed = response.Content.Trim();
            var normalized = trimmed.ToUpperInvariant();
            var isPass = normalized.StartsWith("PASS") || normalized.StartsWith("OK");
            var isFail = normalized.StartsWith("FAIL");
            var isPartial = normalized.StartsWith("PARTIAL");
            var requiresManualReview = isFail || isPartial;
            var score = isPass ? 0.9 : isPartial ? 0.6 : 0.3;
            var status = isPass ? "PASS" : isPartial ? "PARTIAL" : "FAIL";

            _logger.LogInformation(
                "Validation completed for GEM {GemId}. Tokens {TokensUsed}, Cost {Cost}, DurationMs {DurationMs}, Retries {RetryCount}, Result {Result}",
                context.GEMId,
                response.TokensUsed,
                response.CostEstimate,
                stopwatch.ElapsedMilliseconds,
                response.RetryCount,
                status);

            return new AgentResult(
                true,
                isPass ? "Validation succeeded" : "Validation flagged issues",
                new AgentResultData(
                    Name,
                    DateTimeOffset.UtcNow,
                    new Dictionary<string, object>
                    {
                        { "response", response.Content },
                        { "status", status }
                    }),
                new AgentMetrics(
                    response.TokensUsed,
                    response.CostEstimate,
                    stopwatch.Elapsed,
                    response.RetryCount,
                    response.Provider),
                null,
                new AgentResultConfidence(score, requiresManualReview, response.Content));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validation failed for GEM {GemId}", context.GEMId);
            return BuildFailure(context, ex.Message, stopwatch.Elapsed);
        }
    }

    public Task<ValidationResult> ValidateAsync(string content)
    {
        var result = new ValidationResult(true, Array.Empty<string>(), DateTimeOffset.UtcNow);
        return Task.FromResult(result);
    }

    private AgentResult BuildFailure(AgentContext context, string message, TimeSpan duration)
    {
        return new AgentResult(
            false,
            "Validation failed",
            new AgentResultData(
                Name,
                DateTimeOffset.UtcNow,
                new Dictionary<string, object>
                {
                    { "message", message }
                }),
            new AgentMetrics(0, 0m, duration, 0, "unknown"),
            new List<string> { message },
            new AgentResultConfidence(0.2, true, "Validation failed"));
    }
}

public sealed record ValidationResult(
    bool IsValid,
    IReadOnlyList<string> Issues,
    DateTimeOffset EvaluatedAt);
