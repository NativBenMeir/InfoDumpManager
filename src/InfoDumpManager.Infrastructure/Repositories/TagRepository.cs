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

public sealed class TagRepository : ITagRepository
{
    private readonly ApplicationDbContext _context;

    public TagRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Tag tag, CancellationToken cancellationToken = default)
    {
        if (tag is null)
        {
            throw new ArgumentNullException(nameof(tag));
        }

        await _context.Tags.AddAsync(tag, cancellationToken).ConfigureAwait(false);
    }

    public Task<Tag?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.Tags
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<Tag?> GetByNameAsync(Guid tenantId, string name, CancellationToken cancellationToken = default)
    {
        return _context.Tags
            .SingleOrDefaultAsync(
                x => x.TenantId == tenantId && x.Name == name,
                cancellationToken);
    }

    public async Task<IReadOnlyCollection<Tag>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tags = await _context.Tags
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return tags;
    }

    public async Task<IReadOnlyCollection<Tag>> ListByIdsAsync(
        Guid tenantId,
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var idList = ids?.Distinct().ToList() ?? new List<Guid>();
        if (idList.Count == 0)
        {
            return Array.Empty<Tag>();
        }

        var tags = await _context.Tags
            .Where(x => x.TenantId == tenantId && idList.Contains(x.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return tags;
    }
}
