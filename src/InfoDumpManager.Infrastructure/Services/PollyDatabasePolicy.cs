using System;
using System.Threading;
using System.Threading.Tasks;
using InfoDumpManager.Application.Common.Services;
using Polly;

namespace InfoDumpManager.Infrastructure.Services;

public sealed class PollyDatabasePolicy : IDatabasePolicy
{
    private readonly IAsyncPolicy _policy;

    public PollyDatabasePolicy(IAsyncPolicy policy)
    {
        _policy = policy;
    }

    public Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        return _policy.ExecuteAsync(_ => action(), cancellationToken);
    }

    public Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        return _policy.ExecuteAsync(_ => action(), cancellationToken);
    }
}
