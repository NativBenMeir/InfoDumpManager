namespace InfoDumpManager.Application.Services.LLM;

public sealed record LLMRateLimitOptions(
    int PermitLimitPerMinute = 60,
    int QueueLimit = 0);
