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

public sealed class CategorySuggestionRepository : ICategorySuggestionRepository
{
    private readonly ApplicationDbContext _context;

    public CategorySuggestionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(CategorySuggestion suggestion, CancellationToken cancellationToken = default)
    {
        if (suggestion is null)
        {
            throw new ArgumentNullException(nameof(suggestion));
        }

        await _context.CategorySuggestions.AddAsync(suggestion, cancellationToken).ConfigureAwait(false);
    }

    public Task<CategorySuggestion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.CategorySuggestions
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<CategorySuggestion>> ListByGemAsync(
        Guid gemId,
        CancellationToken cancellationToken = default)
    {
        var items = await _context.CategorySuggestions
            .Where(x => x.GEMId == gemId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return items;
    }
}
