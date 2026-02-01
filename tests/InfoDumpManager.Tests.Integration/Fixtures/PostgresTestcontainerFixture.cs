using System;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Configurations;
using Testcontainers.PostgreSql;
using InfoDumpManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoDumpManager.Tests.Integration.Fixtures;

[CollectionDefinition("IntegrationTests")]
public sealed class IntegrationTestCollection :
    ICollectionFixture<PostgresTestcontainerFixture>,
    ICollectionFixture<MinioTestcontainerFixture>
{
}

public sealed class PostgresTestcontainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;

    public PostgresTestcontainerFixture()
    {
        _container = new PostgreSqlBuilder("postgres:16.11")
            .WithDatabase("InfoDumpManagerIntegration")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithCleanUp(true)
            .WithName($"idm-integration-{Guid.NewGuid():N}")
            .Build();
    }

    public string ConnectionString => _container.GetConnectionString();

    public ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString, options => options.EnableRetryOnFailure())
            .EnableSensitiveDataLogging()
            .Options;

        return new ApplicationDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.StopAsync();
        await _container.DisposeAsync();
    }
}

public sealed class MinioTestcontainerFixture : IAsyncLifetime
{
    private const string AccessKey = "minioadmin";
    private const string SecretKey = "minioadmin123";
    private readonly IContainer _container;

    public MinioTestcontainerFixture()
    {
        _container = new ContainerBuilder()
            .WithImage("minio/minio:latest")
            .WithName($"idm-minio-{Guid.NewGuid():N}")
            .WithEnvironment("MINIO_ROOT_USER", AccessKey)
            .WithEnvironment("MINIO_ROOT_PASSWORD", SecretKey)
            .WithCommand("server", "/data", "--console-address", ":9001")
            .WithPortBinding(9000, true)
            .WithCleanUp(true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request =>
                request.ForPort(9000).ForPath("/minio/health/ready")))
            .Build();
    }

    public string Endpoint => $"{_container.Hostname}:{_container.GetMappedPublicPort(9000)}";

    public string BucketName => "gem-snapshots";

    public string UserName => AccessKey;

    public string Password => SecretKey;

    public Task InitializeAsync() => _container.StartAsync();

    public async Task DisposeAsync()
    {
        await _container.StopAsync();
        await _container.DisposeAsync();
    }
}
