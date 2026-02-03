namespace InfoDumpManager.Application.Services.CostManagement;

/// <summary>
/// Repository for cost usage persistence.
/// </summary>
public interface ICostUsageRepository
{
    Task AddAsync(CostUsageRecord record, CancellationToken cancellationToken = default);

    Task<decimal> GetTotalCostAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
}
