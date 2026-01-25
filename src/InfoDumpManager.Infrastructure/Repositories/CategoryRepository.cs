using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InfoDumpManager.Infrastructure.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly ApplicationDbContext _context;

    public CategoryRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Categories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var categories = await _context.Categories
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return categories.AsReadOnly();
    }

    public async Task AddAsync(Category category, CancellationToken cancellationToken = default)
    {
        await _context.Categories.AddAsync(category, cancellationToken);
    }

    public Task UpdateAsync(Category category, CancellationToken cancellationToken = default)
    {
        _context.Categories.Update(category);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Category category, CancellationToken cancellationToken = default)
    {
        _context.Categories.Remove(category);
        return Task.CompletedTask;
    }
}
