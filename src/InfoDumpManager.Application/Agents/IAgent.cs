namespace InfoDumpManager.Application.Agents;

/// <summary>
/// Base interface for all AI agents.
/// </summary>
public interface IAgent
{
    /// <summary>
    /// Gets the agent name used for telemetry and tracing.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the agent capability.
    /// </summary>
    AgentCapability Capability { get; }

    /// <summary>
    /// Executes the agent's operation.
    /// </summary>
    /// <param name="context">Execution context.</param>
    /// <returns>Agent execution result.</returns>
    Task<AgentResult> ExecuteAsync(AgentContext context);
}
