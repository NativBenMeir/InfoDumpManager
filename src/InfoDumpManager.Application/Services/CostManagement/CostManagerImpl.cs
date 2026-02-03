using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfoDumpManager.Application.Services.CostManagement;

/// <summary>
/// Default implementation of cost management.
/// </summary>
public sealed class CostManagerImpl : ICostManager
{
    private readonly ICostUsageRepository _usageRepository;
    private readonly CostManagementOptions _options;
    private readonly ILogger<CostManagerImpl> _logger;

    public CostManagerImpl(
        ICostUsageRepository usageRepository,
        IOptions<CostManagementOptions> options,
        ILogger<CostManagerImpl> logger)
    {
        _usageRepository = usageRepository;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CostCheckResult> CanProcessAsync(
        Guid tenantId,
        int estimatedTokens,
        string operation,
        CancellationToken cancellationToken = default)
    {
        if (estimatedTokens < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(estimatedTokens), "Estimated tokens cannot be negative.");
        }

        var now = DateTimeOffset.UtcNow;
        var periodStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var periodEnd = periodStart.AddMonths(1);

        var costPer1K = ResolveCostPer1K(operation);
        var estimatedCost = (estimatedTokens / 1000m) * costPer1K;
        var totalCost = await _usageRepository
            .GetTotalCostAsync(tenantId, periodStart, periodEnd, cancellationToken)
            .ConfigureAwait(false);

        var remaining = _options.MonthlyBudgetUsd - totalCost;
        var allowed = remaining >= estimatedCost;

        if (!allowed)
        {
            _logger.LogWarning(
                "Budget exceeded for tenant {TenantId}. Remaining {Remaining}, Estimated {Estimated}",
                tenantId,
                remaining,
                estimatedCost);
        }

        return new CostCheckResult(
            Allowed: allowed,
            EstimatedCost: estimatedCost,
            RemainingBudget: remaining,
            Reason: allowed ? "BudgetAvailable" : "BudgetExceeded",
            Message: allowed
                ? "Budget allowed."
                : "Budget exceeded for the current period.");
    }

    public Task RecordUsageAsync(
        Guid tenantId,
        Guid gemId,
        string operation,
        int tokensUsed,
        decimal cost,
        CancellationToken cancellationToken = default)
    {
        if (tokensUsed < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tokensUsed), "Tokens used cannot be negative.");
        }

        if (cost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cost), "Cost cannot be negative.");
        }

        var record = new CostUsageRecord(
            Guid.NewGuid(),
            tenantId,
            gemId,
            operation,
            tokensUsed,
            cost,
            DateTimeOffset.UtcNow);

        return _usageRepository.AddAsync(record, cancellationToken);
    }

    private decimal ResolveCostPer1K(string operation)
    {
        if (_options.OperationCostPer1KTokensUsd.TryGetValue(operation, out var value))
        {
            return value;
        }

        return _options.DefaultCostPer1KTokensUsd;
    }
}
