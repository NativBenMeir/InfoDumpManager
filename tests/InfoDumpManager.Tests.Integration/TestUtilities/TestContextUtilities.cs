using System;
using System.Threading;
using System.Threading.Tasks;
using InfoDumpManager.Application.Common.Services;
using InfoDumpManager.Domain.Repositories;

namespace InfoDumpManager.Tests.Integration.TestUtilities;

/// <summary>
/// Mock implementation of ICurrentUserContext for testing.
/// </summary>
public sealed class TestCurrentUserContext : ICurrentUserContext
{
    public Guid UserId { get; } = Guid.NewGuid();
    public Guid TenantId { get; } = Guid.NewGuid();
    public bool IsAuthenticated => true;
}

/// <summary>
/// No-operation database policy for testing without actual retry/circuit breaker logic.
/// </summary>
public sealed class NoOpDatabasePolicy : IDatabasePolicy
{
    public Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        return action();
    }

    public Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        return action();
    }
}
