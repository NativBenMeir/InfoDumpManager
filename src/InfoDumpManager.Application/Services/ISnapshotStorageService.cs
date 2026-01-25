using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace InfoDumpManager.Application.Services;

public interface ISnapshotStorageService
{
    Task<Uri> StoreSnapshotAsync(string objectName, Stream data, string contentType, CancellationToken cancellationToken = default);
}
