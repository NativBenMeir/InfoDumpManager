using System;
using System.Threading;
using System.Threading.Tasks;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Infrastructure.Data;

namespace InfoDumpManager.Infrastructure.Repositories;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;

    public UnitOfWork(
        ApplicationDbContext context,
        IGEMRepository gemRepository,
        ICategoryRepository categoryRepository,
        ITagRepository tagRepository,
        ICategorySuggestionRepository categorySuggestionRepository,
        IActivityLogRepository activityLogRepository)
    {
        _context = context;
        GEMs = gemRepository;
        Categories = categoryRepository;
        Tags = tagRepository;
        CategorySuggestions = categorySuggestionRepository;
        ActivityLogs = activityLogRepository;
    }

    public IGEMRepository GEMs { get; }
    public ICategoryRepository Categories { get; }
    public ITagRepository Tags { get; }
    public ICategorySuggestionRepository CategorySuggestions { get; }
    public IActivityLogRepository ActivityLogs { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    public ValueTask DisposeAsync()
        => _context.DisposeAsync();
}
