namespace InfoDumpManager.Application.Services.CostManagement;

/// <summary>
/// Budget enforcement and usage tracking for AI calls.
/// </summary>
public interface ICostManager
{
    Task<CostCheckResult> CanProcessAsync(
        Guid tenantId,
        int estimatedTokens,
        string operation,
        CancellationToken cancellationToken = default);

    Task RecordUsageAsync(
        Guid tenantId,
        Guid gemId,
        string operation,
        int tokensUsed,
        decimal cost,
        CancellationToken cancellationToken = default);
}
