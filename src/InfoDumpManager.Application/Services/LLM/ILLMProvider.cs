namespace InfoDumpManager.Application.Services.LLM;

/// <summary>
/// Abstraction for large language model calls.
/// </summary>
public interface ILLMProvider
{
    /// <summary>
    /// Executes an LLM prompt and returns the response.
    /// </summary>
    /// <param name="prompt">Prompt content.</param>
    /// <param name="model">Model identifier.</param>
    /// <param name="maxTokens">Maximum output tokens.</param>
    /// <param name="temperature">Sampling temperature.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>LLM response.</returns>
    Task<LLMResponse> CallAsync(
        string prompt,
        string model,
        int maxTokens,
        float temperature,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, string>? promptVariables = null);

    /// <summary>
    /// Executes an LLM prompt for an explicit provider and returns the response.
    /// </summary>
    /// <param name="prompt">Prompt content.</param>
    /// <param name="provider">Provider identifier (e.g. OpenAI, AzureOpenAI).</param>
    /// <param name="model">Model identifier.</param>
    /// <param name="maxTokens">Maximum output tokens.</param>
    /// <param name="temperature">Sampling temperature.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="promptVariables">Optional prompt template variables.</param>
    /// <returns>LLM response.</returns>
    Task<LLMResponse> CallAsync(
        string prompt,
        string provider,
        string model,
        int maxTokens,
        float temperature,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, string>? promptVariables = null);
}
