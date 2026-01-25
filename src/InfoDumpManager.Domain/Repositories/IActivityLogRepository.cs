using System.Threading;
using System.Threading.Tasks;
using InfoDumpManager.Domain.Entities;

namespace InfoDumpManager.Domain.Repositories;

public interface IActivityLogRepository
{
    Task AddAsync(ActivityLog logEntry, CancellationToken cancellationToken = default);
}
