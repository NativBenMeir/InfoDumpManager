using System.Threading;
using System.Threading.Tasks;

namespace InfoDumpManager.Domain.Common;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
