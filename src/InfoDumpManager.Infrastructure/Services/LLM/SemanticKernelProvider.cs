using InfoDumpManager.Application.Services.LLM;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Polly;

namespace InfoDumpManager.Infrastructure.Services.LLM;

/// <summary>
/// Semantic Kernel backed LLM provider with Polly resilience.
/// </summary>
public sealed class SemanticKernelProvider : ILLMProvider
{
    private readonly Kernel _kernel;
    private readonly ILogger<SemanticKernelProvider> _logger;
    private readonly IAsyncPolicy<LLMResponse> _policy;

    public SemanticKernelProvider(Kernel kernel, ILogger<SemanticKernelProvider> logger)
    {
        _kernel = kernel;
        _logger = logger;
        _policy = BuildPolicy();
    }

    public Task<LLMResponse> CallAsync(
        string prompt,
        string model,
        int maxTokens,
        float temperature,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Prompt cannot be empty.", nameof(prompt));
        }

        return _policy.ExecuteAsync(ct => ExecuteInternalAsync(prompt, model, maxTokens, temperature, ct), cancellationToken);
    }

    private async Task<LLMResponse> ExecuteInternalAsync(
        string prompt,
        string model,
        int maxTokens,
        float temperature,
        CancellationToken cancellationToken)
    {
        var retryCount = 0;
        var arguments = new KernelArguments
        {
            ["model"] = model,
            ["max_tokens"] = maxTokens,
            ["temperature"] = temperature
        };

        var result = await _kernel.InvokePromptAsync(prompt, arguments, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var content = result.GetValue<string>() ?? string.Empty;

        return new LLMResponse(
            content,
            model,
            "SemanticKernel",
            TokensUsed: 0,
            CostEstimate: 0m,
            FinishReason: "completed",
            RetryCount: retryCount);
    }

    private IAsyncPolicy<LLMResponse> BuildPolicy()
    {
        var retryPolicy = Policy<LLMResponse>
            .Handle<Exception>()
            .WaitAndRetryAsync(
                3,
                attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                (outcome, delay, attempt, _) =>
                {
                    if (outcome.Exception is not null)
                    {
                        _logger.LogWarning(outcome.Exception, "LLM retry attempt {Attempt} after {Delay}", attempt, delay);
                    }
                    else
                    {
                        _logger.LogWarning("LLM retry attempt {Attempt} after {Delay}", attempt, delay);
                    }
                });

        var circuitBreaker = Policy<LLMResponse>
            .Handle<Exception>()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

        return Policy.WrapAsync(retryPolicy, circuitBreaker);
    }
}
