using System;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Configurations;
using Testcontainers.PostgreSql;
using InfoDumpManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
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
        _container = new PostgreSqlBuilder("pgvector/pgvector:pg16")
            .WithDatabase("InfoDumpManagerIntegration")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithCleanUp(true)
            .WithName($"idm-integration-{Guid.NewGuid():N}")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("pg_isready", "-h", "localhost"))
            .Build();
    }

    public string ConnectionString => _container.GetConnectionString();

    public ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString, opts => {
                opts.EnableRetryOnFailure();
                opts.UseVector();
            })
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .EnableSensitiveDataLogging()
            .Options;

        return new ApplicationDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var adminBuilder = new Npgsql.NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Database = "postgres"
        };

        await using (var adminConnection = new Npgsql.NpgsqlConnection(adminBuilder.ConnectionString))
        {
            await adminConnection.OpenAsync();
            await using var existsCommand = adminConnection.CreateCommand();
            existsCommand.CommandText = "SELECT 1 FROM pg_database WHERE datname = @db";
            existsCommand.Parameters.AddWithValue("db", "InfoDumpManagerIntegration");
            var exists = await existsCommand.ExecuteScalarAsync();
            if (exists is null)
            {
                await using var createCommand = adminConnection.CreateCommand();
                createCommand.CommandText = "CREATE DATABASE \"InfoDumpManagerIntegration\"";
                await createCommand.ExecuteNonQueryAsync();
            }
        }

        await using (var connection = new Npgsql.NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "CREATE EXTENSION IF NOT EXISTS vector;";
            await command.ExecuteNonQueryAsync();
            await connection.ReloadTypesAsync();
        }
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
        _container = new ContainerBuilder("minio/minio:latest")
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
