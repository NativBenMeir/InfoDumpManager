using System;
using System.Threading;
using System.Threading.Tasks;

namespace InfoDumpManager.Application.Common.Services;

public interface IDatabasePolicy
{
    Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken = default);

    Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default);
}
