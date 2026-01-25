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

public class GEMRepository : IGEMRepository
{
    private readonly ApplicationDbContext _context;

    public GEMRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<GEM?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Gems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<GEM> Items, int Total)> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var total = await _context.Gems.CountAsync(cancellationToken);
        var gems = await _context.Gems
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (gems.AsReadOnly(), total);
    }

    public async Task AddAsync(GEM gem, CancellationToken cancellationToken = default)
    {
        await _context.Gems.AddAsync(gem, cancellationToken);
    }

    public Task UpdateAsync(GEM gem, CancellationToken cancellationToken = default)
    {
        _context.Gems.Update(gem);
        return Task.CompletedTask;
    }
}
