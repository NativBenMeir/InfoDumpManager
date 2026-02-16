using InfoDumpManager.Application.Common.Services;
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
    private readonly IReadOnlyDictionary<string, Kernel> _kernels;
    private readonly ILogger<SemanticKernelProvider> _logger;
    private readonly IAsyncPolicy<LLMResponse> _policy;

    public SemanticKernelProvider(
        IReadOnlyDictionary<string, Kernel> kernels,
        IResiliencePolicyProvider resilienceProvider,
        ILogger<SemanticKernelProvider> logger)
    {
        _kernels = kernels;
        _logger = logger;
        _policy = resilienceProvider.GetLLMPolicy<LLMResponse>();
    }

    public Task<LLMResponse> CallAsync(
        string prompt,
        string model,
        int maxTokens,
        float temperature,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, string>? promptVariables = null)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Prompt cannot be empty.", nameof(prompt));
        }

        throw new InvalidOperationException(
            "Provider-specific LLM calls are required. Use the CallAsync overload with an explicit provider.");
    }

    public Task<LLMResponse> CallAsync(
        string prompt,
        string provider,
        string model,
        int maxTokens,
        float temperature,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, string>? promptVariables = null)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Prompt cannot be empty.", nameof(prompt));
        }

        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new InvalidOperationException("LLM provider must be configured for this call.");
        }

        if (!_kernels.TryGetValue(provider, out var kernel))
        {
            throw new InvalidOperationException($"LLM provider '{provider}' is not registered.");
        }

        return _policy.ExecuteAsync(
            ct => ExecuteInternalAsync(kernel, prompt, model, provider, maxTokens, temperature, promptVariables, ct),
            cancellationToken);
    }

    private async Task<LLMResponse> ExecuteInternalAsync(
        Kernel kernel,
        string prompt,
        string model,
        string provider,
        int maxTokens,
        float temperature,
        IReadOnlyDictionary<string, string>? promptVariables,
        CancellationToken cancellationToken)
    {
        var retryCount = 0;
        var arguments = new KernelArguments
        {
            ["model"] = model,
            ["max_tokens"] = maxTokens,
            ["temperature"] = temperature
        };

        var template = prompt;
        if (promptVariables is null || promptVariables.Count == 0)
        {
            // Defensive guard for legacy interpolated prompts that may include user text with
            // mustache-like template markers (e.g. {{themeMenuId}}).
            template = EscapeTemplateMarkers(prompt);
        }
        else
        {
            foreach (var (key, value) in promptVariables)
            {
                arguments[key] = EscapeTemplateMarkers(value);
            }
        }

        var result = await kernel.InvokePromptAsync(template, arguments, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var content = result.GetValue<string>() ?? string.Empty;

        return new LLMResponse(
            content,
            model,
            provider,
            TokensUsed: 0,
            CostEstimate: 0m,
            FinishReason: "completed",
            RetryCount: retryCount);
    }

    private static string EscapeTemplateMarkers(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value
            .Replace("{{", "{ {", StringComparison.Ordinal)
            .Replace("}}", "} }", StringComparison.Ordinal);
    }
}
