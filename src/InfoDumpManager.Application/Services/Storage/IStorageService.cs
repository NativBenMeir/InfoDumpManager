using System.Threading;
using System.Threading.Tasks;

namespace InfoDumpManager.Application.Services.Storage;

/// <summary>
/// Abstraction for object storage operations (e.g., MinIO, S3).
/// </summary>
public interface IStorageService
{
    Task<string> UploadSnapshotAsync(string objectKey, string htmlContent, string contentType, CancellationToken cancellationToken = default);
    Task<string> GetSnapshotAsync(string objectKey, CancellationToken cancellationToken = default);
}
