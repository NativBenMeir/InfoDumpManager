using System;
using System.Threading;
using System.Threading.Tasks;

namespace InfoDumpManager.Domain.Repositories;

public interface IUnitOfWork : IAsyncDisposable
{
    IGEMRepository GEMs { get; }
    ICategoryRepository Categories { get; }
    IActivityLogRepository ActivityLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
