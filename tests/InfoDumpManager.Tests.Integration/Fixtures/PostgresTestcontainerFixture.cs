using System;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using Testcontainers.PostgreSql;
using InfoDumpManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoDumpManager.Tests.Integration.Fixtures;

[CollectionDefinition("IntegrationTests")]
public sealed class IntegrationTestCollection : ICollectionFixture<PostgresTestcontainerFixture>
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
