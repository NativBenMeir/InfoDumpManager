using InfoDumpManager.Application.Common.Services;
using Microsoft.Extensions.Logging;
using Polly;

namespace InfoDumpManager.Infrastructure.Services;

/// <summary>
/// Central Polly policy provider for all resilience scenarios.
/// </summary>
public sealed class PollyResiliencePolicyProvider : IResiliencePolicyProvider
{
    private readonly ILogger<PollyResiliencePolicyProvider> _logger;

    public PollyResiliencePolicyProvider(ILogger<PollyResiliencePolicyProvider> logger)
    {
        _logger = logger;

        var dbRetry = Policy.Handle<Exception>()
            .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                (ex, delay, attempt, _) =>
                    _logger.LogWarning(ex, "Database retry {Attempt} after {Delay}", attempt, delay));

        var dbBreaker = Policy.Handle<Exception>()
            .CircuitBreakerAsync(2, TimeSpan.FromSeconds(30),
                (ex, duration) => _logger.LogWarning(ex, "Database circuit opened for {Duration}", duration),
                () => _logger.LogInformation("Database circuit closed"));

        DatabasePolicy = Policy.WrapAsync(dbRetry, dbBreaker);
    }

    public IAsyncPolicy DatabasePolicy { get; }

    public IAsyncPolicy<T> GetLLMPolicy<T>()
    {
        var retry = Policy<T>.Handle<Exception>()
            .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                (outcome, delay, attempt, _) =>
                {
                    if (outcome.Exception is not null)
                    {
                        _logger.LogWarning(outcome.Exception, "LLM retry {Attempt} after {Delay}", attempt, delay);
                    }
                    else
                    {
                        _logger.LogWarning("LLM retry {Attempt} after {Delay}", attempt, delay);
                    }
                });

        var breaker = Policy<T>.Handle<Exception>()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

        return Policy.WrapAsync(retry, breaker);
    }
}
