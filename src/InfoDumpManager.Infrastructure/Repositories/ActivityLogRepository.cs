using System.Threading;
using System.Threading.Tasks;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Infrastructure.Data;

namespace InfoDumpManager.Infrastructure.Repositories;

public class ActivityLogRepository : IActivityLogRepository
{
    private readonly ApplicationDbContext _context;

    public ActivityLogRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ActivityLog logEntry, CancellationToken cancellationToken = default)
    {
        await _context.ActivityLogs.AddAsync(logEntry, cancellationToken);
    }
}
