namespace InfoDumpManager.Application.Services.CostManagement;

/// <summary>
/// Budget check result.
/// </summary>
/// <param name="Allowed">Whether the operation is allowed.</param>
/// <param name="EstimatedCost">Estimated cost for the operation.</param>
/// <param name="RemainingBudget">Remaining budget.</param>
/// <param name="Reason">Reason code.</param>
/// <param name="Message">Message for diagnostics.</param>
public sealed record CostCheckResult(
    bool Allowed,
    decimal EstimatedCost,
    decimal RemainingBudget,
    string Reason,
    string Message);

/// <summary>
/// Cost usage record.
/// </summary>
/// <param name="Id">Usage identifier.</param>
/// <param name="TenantId">Tenant identifier.</param>
/// <param name="GEMId">GEM identifier.</param>
/// <param name="Operation">Operation name.</param>
/// <param name="TokensUsed">Tokens used.</param>
/// <param name="Cost">Cost in USD.</param>
/// <param name="CreatedAt">Timestamp.</param>
public sealed record CostUsageRecord(
    Guid Id,
    Guid TenantId,
    Guid GEMId,
    string Operation,
    int TokensUsed,
    decimal Cost,
    DateTimeOffset CreatedAt);

/// <summary>
/// Cost management options.
/// </summary>
public sealed class CostManagementOptions
{
    public decimal MonthlyBudgetUsd { get; set; } = 100m;

    public decimal DefaultCostPer1KTokensUsd { get; set; } = 0.01m;

    public Dictionary<string, decimal> OperationCostPer1KTokensUsd { get; set; } = new();
}
