namespace InfoDumpManager.Application.Services.LLM;

/// <summary>
/// Strongly typed LLM configuration for agent-specific model/provider selection.
/// </summary>
public sealed class AgentLlmSettings
{
    public Dictionary<string, AgentLlmAgentSettings> Agents { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    public AgentLlmAgentSettings GetRequiredAgent(string agentName)
    {
        if (string.IsNullOrWhiteSpace(agentName))
        {
            throw new ArgumentException("Agent name is required.", nameof(agentName));
        }

        if (!Agents.TryGetValue(agentName, out var config))
        {
            throw new InvalidOperationException($"LLM configuration is missing for agent '{agentName}'.");
        }

        return config;
    }
}

public sealed class AgentLlmAgentSettings
{
    public LlmEndpointSettings Chat { get; init; } = new();

    public LlmEndpointSettings Embedding { get; init; } = new();
}

public sealed class LlmEndpointSettings
{
    public string Provider { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;
}
