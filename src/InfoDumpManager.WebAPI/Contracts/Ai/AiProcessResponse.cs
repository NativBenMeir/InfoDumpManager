using InfoDumpManager.Application.Agents.Orchestration;

namespace InfoDumpManager.WebAPI.Contracts.Ai;

public sealed class AiProcessResponse
{
    public Guid JobId { get; set; }

    public ProcessingStatus Status { get; set; }
}
