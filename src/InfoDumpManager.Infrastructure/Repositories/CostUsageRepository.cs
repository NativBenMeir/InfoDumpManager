using InfoDumpManager.Application.Services.CostManagement;
using InfoDumpManager.Infrastructure.Data;
using InfoDumpManager.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace InfoDumpManager.Infrastructure.Repositories;

public sealed class CostUsageRepository : ICostUsageRepository
{
    private readonly ApplicationDbContext _context;

    public CostUsageRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(CostUsageRecord record, CancellationToken cancellationToken = default)
    {
        if (record is null)
        {
            throw new ArgumentNullException(nameof(record));
        }

        var entity = new CostUsageEntry
        {
            Id = record.Id,
            TenantId = record.TenantId,
            GEMId = record.GEMId,
            Operation = record.Operation,
            TokensUsed = record.TokensUsed,
            Cost = record.Cost,
            CreatedAt = record.CreatedAt
        };

        await _context.CostUsageEntries.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<decimal> GetTotalCostAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        return _context.CostUsageEntries
            .Where(x => x.TenantId == tenantId && x.CreatedAt >= from && x.CreatedAt < to)
            .SumAsync(x => x.Cost, cancellationToken);
    }
}
