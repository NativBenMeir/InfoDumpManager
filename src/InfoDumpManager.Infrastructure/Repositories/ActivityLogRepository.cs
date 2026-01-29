using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InfoDumpManager.Infrastructure.Repositories;

public sealed class ActivityLogRepository : IActivityLogRepository
{
    private readonly ApplicationDbContext _context;

    public ActivityLogRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ActivityLog activityLog, CancellationToken cancellationToken = default)
    {
        if (activityLog is null)
        {
            throw new ArgumentNullException(nameof(activityLog));
        }

        await _context.ActivityLogs.AddAsync(activityLog, cancellationToken).ConfigureAwait(false);
    }

    public Task<ActivityLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.ActivityLogs
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<ActivityLog>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var logs = await _context.ActivityLogs
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.OccurredAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return logs;
    }
}
