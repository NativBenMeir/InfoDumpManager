using InfoDumpManager.Application.Services.LLM;
using Microsoft.Extensions.Options;

namespace InfoDumpManager.Infrastructure.Services.LLM;

/// <summary>
/// Validates required per-agent LLM configuration at application startup.
/// </summary>
public sealed class AgentLlmSettingsValidator : IValidateOptions<AgentLlmSettings>
{
    private static readonly string[] RequiredAgents =
    [
        "SummarizationAgent",
        "CategorizationAgent",
        "TaggingAgent",
        "ValidationAgent"
    ];

    public ValidateOptionsResult Validate(string? name, AgentLlmSettings options)
    {
        if (options is null)
        {
            return ValidateOptionsResult.Fail("LLM settings are missing.");
        }

        var failures = new List<string>();

        foreach (var agent in RequiredAgents)
        {
            if (!options.Agents.TryGetValue(agent, out var settings))
            {
                failures.Add($"LLM:Agents:{agent} is required.");
                continue;
            }

            ValidateEndpoint($"LLM:Agents:{agent}:Chat", settings.Chat, failures, validateProvider: true);
            ValidateEndpoint($"LLM:Agents:{agent}:Embedding", settings.Embedding, failures, validateProvider: false);
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static void ValidateEndpoint(
        string path,
        LlmEndpointSettings settings,
        List<string> failures,
        bool validateProvider)
    {
        if (settings is null)
        {
            failures.Add($"{path} is required.");
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.Provider))
        {
            failures.Add($"{path}:Provider is required.");
        }
        else if (validateProvider
            && !settings.Provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase)
            && !settings.Provider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add($"{path}:Provider must be OpenAI or AzureOpenAI.");
        }

        if (string.IsNullOrWhiteSpace(settings.Model))
        {
            failures.Add($"{path}:Model is required.");
        }
    }
}
