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

public sealed class GEMRepository : IGEMRepository
{
    private readonly ApplicationDbContext _context;

    public GEMRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(GEM gem, CancellationToken cancellationToken = default)
    {
        if (gem is null)
        {
            throw new ArgumentNullException(nameof(gem));
        }

        await _context.Gems.AddAsync(gem, cancellationToken).ConfigureAwait(false);
    }

    public Task<GEM?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.Gems
            .Include(x => x.Category)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<GEM?> GetByUrlAsync(Guid tenantId, string url, CancellationToken cancellationToken = default)
    {
        return _context.Gems
            .Include(x => x.Category)
            .SingleOrDefaultAsync(
                x => x.TenantId == tenantId && x.Url == url,
                cancellationToken);
    }

    public async Task<IReadOnlyCollection<GEM>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var gems = await _context.Gems
            .Where(x => x.TenantId == tenantId)
            .Include(x => x.Category)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return gems;
    }

    public async Task<IReadOnlyCollection<GEM>> ListByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        var gems = await _context.Gems
            .Where(x => x.CategoryId == categoryId)
            .Include(x => x.Category)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return gems;
    }

    public Task<bool> ExistsByUrlAsync(Guid tenantId, string url, CancellationToken cancellationToken = default)
    {
        return _context.Gems.AnyAsync(x => x.TenantId == tenantId && x.Url == url, cancellationToken);
    }
}
