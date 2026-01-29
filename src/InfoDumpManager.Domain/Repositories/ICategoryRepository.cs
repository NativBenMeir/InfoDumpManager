using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InfoDumpManager.Domain.Entities;

namespace InfoDumpManager.Domain.Repositories;

public interface ICategoryRepository
{
    Task AddAsync(Category category, CancellationToken cancellationToken = default);
    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Category?> GetByNameAsync(Guid tenantId, string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Category>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(Guid tenantId, string name, CancellationToken cancellationToken = default);
}
