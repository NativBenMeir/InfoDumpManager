namespace InfoDumpManager.Application.Services.LLM;

/// <summary>
/// Standardized response from an LLM provider.
/// </summary>
/// <param name="Content">Generated content.</param>
/// <param name="Model">Model identifier used for generation.</param>
/// <param name="Provider">Provider name.</param>
/// <param name="TokensUsed">Total tokens used.</param>
/// <param name="CostEstimate">Estimated cost in USD.</param>
/// <param name="FinishReason">Completion reason.</param>
/// <param name="RetryCount">Retries applied by resilience policies.</param>
public sealed record LLMResponse(
    string Content,
    string Model,
    string Provider,
    int TokensUsed,
    decimal CostEstimate,
    string FinishReason,
    int RetryCount);
