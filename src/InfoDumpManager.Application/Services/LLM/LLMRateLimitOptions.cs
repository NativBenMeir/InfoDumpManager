namespace InfoDumpManager.Application.Services.LLM;

public sealed class LLMRateLimitOptions
{
    public int PermitLimitPerMinute { get; init; } = 60;
    public int QueueLimit { get; init; } = 0;
}
