using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InfoDumpManager.Domain.Entities;

namespace InfoDumpManager.Domain.Repositories;

public interface IGEMRepository
{
    Task AddAsync(GEM gem, CancellationToken cancellationToken = default);
    Task<GEM?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<GEM?> GetByUrlAsync(Guid tenantId, string url, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<GEM>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<GEM>> ListByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByUrlAsync(Guid tenantId, string url, CancellationToken cancellationToken = default);
}
