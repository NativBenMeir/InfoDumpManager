using System;
using System.Threading;
using System.Threading.Tasks;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Infrastructure.Data;

namespace InfoDumpManager.Infrastructure.Repositories;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private IGEMRepository? _gemRepository;
    private ICategoryRepository? _categoryRepository;
    private ITagRepository? _tagRepository;
    private ICategorySuggestionRepository? _categorySuggestionRepository;
    private IActivityLogRepository? _activityLogRepository;

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
    }

    public IGEMRepository GEMs => _gemRepository ??= new GEMRepository(_context);
    public ICategoryRepository Categories => _categoryRepository ??= new CategoryRepository(_context);
    public ITagRepository Tags => _tagRepository ??= new TagRepository(_context);
    public ICategorySuggestionRepository CategorySuggestions => _categorySuggestionRepository ??= new CategorySuggestionRepository(_context);
    public IActivityLogRepository ActivityLogs => _activityLogRepository ??= new ActivityLogRepository(_context);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    public ValueTask DisposeAsync()
        => _context.DisposeAsync();
}
