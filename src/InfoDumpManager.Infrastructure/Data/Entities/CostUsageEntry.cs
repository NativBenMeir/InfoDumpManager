namespace InfoDumpManager.Infrastructure.Data.Entities;

public sealed class CostUsageEntry
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid GEMId { get; set; }

    public string Operation { get; set; } = string.Empty;

    public int TokensUsed { get; set; }

    public decimal Cost { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
