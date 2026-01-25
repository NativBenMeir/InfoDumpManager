using System.Threading;
using System.Threading.Tasks;

namespace InfoDumpManager.Application.Services;

public interface IPageSnapshotService
{
    Task<PageSnapshot> CaptureAsync(string url, CancellationToken cancellationToken = default);
}
