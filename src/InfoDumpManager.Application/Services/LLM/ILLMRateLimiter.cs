namespace InfoDumpManager.Application.Services.LLM;

/// <summary>
/// Per-tenant rate limiter for LLM calls.
/// </summary>
public interface ILLMRateLimiter
{
    Task<T> ExecuteAsync<T>(Guid tenantId, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);
}
