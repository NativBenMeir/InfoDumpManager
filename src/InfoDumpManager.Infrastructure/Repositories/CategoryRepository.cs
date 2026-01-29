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

public sealed class CategoryRepository : ICategoryRepository
{
    private readonly ApplicationDbContext _context;

    public CategoryRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Category category, CancellationToken cancellationToken = default)
    {
        if (category is null)
        {
            throw new ArgumentNullException(nameof(category));
        }

        await _context.Categories.AddAsync(category, cancellationToken).ConfigureAwait(false);
    }

    public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.Categories
            .Include(x => x.Gems)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<Category?> GetByNameAsync(Guid tenantId, string name, CancellationToken cancellationToken = default)
    {
        return _context.Categories
            .Include(x => x.Gems)
            .SingleOrDefaultAsync(
                x => x.TenantId == tenantId && x.Name == name,
                cancellationToken);
    }

    public async Task<IReadOnlyCollection<Category>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var categories = await _context.Categories
            .Where(x => x.TenantId == tenantId)
            .Include(x => x.Gems)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return categories;
    }

    public Task<bool> ExistsByNameAsync(Guid tenantId, string name, CancellationToken cancellationToken = default)
    {
        return _context.Categories.AnyAsync(x => x.TenantId == tenantId && x.Name == name, cancellationToken);
    }
}
