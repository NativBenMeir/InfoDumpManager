using Polly;

namespace InfoDumpManager.Application.Common.Services;

/// <summary>
/// Centralized provider for resilience policies.
/// </summary>
public interface IResiliencePolicyProvider
{
    /// <summary>
    /// Non-generic async policy for database operations.
    /// </summary>
    IAsyncPolicy DatabasePolicy { get; }

    /// <summary>
    /// Typed async policy for LLM operations.
    /// </summary>
    IAsyncPolicy<T> GetLLMPolicy<T>();
}
