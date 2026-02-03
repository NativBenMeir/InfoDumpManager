namespace InfoDumpManager.WebAPI.Contracts.Ai;

public sealed class AiProcessRequest
{
    public Guid GemId { get; set; }

    public string? ContentText { get; set; }

    public string Source { get; set; } = "web";

    public double AutoApproveThreshold { get; set; } = 0.7;

    public bool RunValidation { get; set; } = true;

    public int? MaxConcurrentJobs { get; set; }

    public int? TimeoutSeconds { get; set; }
}
