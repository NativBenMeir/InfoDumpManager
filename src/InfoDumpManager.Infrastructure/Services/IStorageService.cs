using System.Threading;
using System.Threading.Tasks;

namespace InfoDumpManager.Infrastructure.Services;

public interface IStorageService
{
    Task<string> UploadSnapshotAsync(string objectKey, string htmlContent, string contentType, CancellationToken cancellationToken = default);
    Task<string> GetSnapshotAsync(string objectKey, CancellationToken cancellationToken = default);
}
