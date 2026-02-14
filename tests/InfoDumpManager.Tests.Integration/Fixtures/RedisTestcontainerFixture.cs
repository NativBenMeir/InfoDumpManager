using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using StackExchange.Redis;
using Xunit;

namespace InfoDumpManager.Tests.Integration.Fixtures;

[CollectionDefinition("RedisIntegrationTests")]
public sealed class RedisIntegrationTestCollection : ICollectionFixture<RedisTestcontainerFixture>
{
}

public sealed class RedisTestcontainerFixture : IAsyncLifetime
{
    private readonly IContainer _container;

    public RedisTestcontainerFixture()
    {
        _container = new ContainerBuilder("redis:7-alpine")
            .WithName($"idm-redis-{Guid.NewGuid():N}")
            .WithPortBinding(6379, true)
            .WithCleanUp(true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("redis-cli", "ping"))
            .Build();
    }

    public string Configuration
    {
        get
        {
            var host = _container.Hostname;
            var port = _container.GetMappedPublicPort(6379);
            return $"{host}:{port},abortConnect=false";
        }
    }

    public IConnectionMultiplexer ConnectionMultiplexer { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionMultiplexer = await StackExchange.Redis.ConnectionMultiplexer.ConnectAsync(Configuration);
    }

    public Task ResetAsync()
    {
        return ConnectionMultiplexer.GetDatabase().ExecuteAsync("FLUSHDB");
    }

    public async Task DisposeAsync()
    {
        if (ConnectionMultiplexer is not null)
        {
            await ConnectionMultiplexer.CloseAsync();
            ConnectionMultiplexer.Dispose();
        }

        await _container.StopAsync();
        await _container.DisposeAsync();
    }
}
