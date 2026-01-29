using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InfoDumpManager.Domain.Entities;

namespace InfoDumpManager.Domain.Repositories;

public interface IActivityLogRepository
{
    Task AddAsync(ActivityLog activityLog, CancellationToken cancellationToken = default);
    Task<ActivityLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ActivityLog>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
