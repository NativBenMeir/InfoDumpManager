using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InfoDumpManager.Domain.Entities;

namespace InfoDumpManager.Domain.Repositories;

public interface IGEMRepository
{
    Task<GEM?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<GEM> Items, int Total)> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task AddAsync(GEM gem, CancellationToken cancellationToken = default);
    Task UpdateAsync(GEM gem, CancellationToken cancellationToken = default);
}
