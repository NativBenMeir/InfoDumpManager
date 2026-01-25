using System;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using InfoDumpManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InfoDumpManager.Tests.Integration.Infrastructure;

public abstract class PostgresIntegrationTestBase : IAsyncLifetime
{
    private readonly PostgreSqlTestcontainer _postgres;
    private string? _skipReason;

    protected PostgresIntegrationTestBase()
    {
        _postgres = new TestcontainersBuilder<PostgreSqlTestcontainer>()
            .WithDatabase(new PostgreSqlTestcontainerConfiguration
            {
                Database = "infodump",
                Username = "postgres",
                Password = "postgres"
            })
            .WithImage("postgres:16-alpine")
            .Build();
    }

    public string? SkipReason => _skipReason;
    protected bool ShouldSkip => _skipReason is not null;

    public async Task InitializeAsync()
    {
        try
        {
            await _postgres.StartAsync();
            await using var context = CreateContext();
            await context.Database.MigrateAsync();
        }
        catch (InvalidOperationException ex)
        {
            _skipReason = $"Testcontainers unavailable: {ex.Message}";
        }
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    protected async Task ResetDatabaseAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }

    protected ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options;

        return new ApplicationDbContext(options);
    }
}
