using System.Diagnostics.CodeAnalysis;
using InfoDumpManager.Application.Services.CostManagement;
using InfoDumpManager.Infrastructure.Data;
using InfoDumpManager.Infrastructure.Data.Entities;
using InfoDumpManager.Infrastructure.Repositories;
using InfoDumpManager.Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoDumpManager.Tests.Integration.AIAgents;

[ExcludeFromCodeCoverage]
[Collection("IntegrationTests")]
public sealed class CostTrackingIntegrationTests : IAsyncLifetime
{
    private readonly PostgresTestcontainerFixture _fixture;
    private ApplicationDbContext _dbContext = null!;

    public CostTrackingIntegrationTests(PostgresTestcontainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _dbContext = _fixture.CreateContext();
        await _dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task RecordUsageAsync_ShouldPersistToDatabase()
    {
        // Arrange
        var repository = new CostUsageRepository(_dbContext);
        var record = new CostUsageRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "summarization",
            250,
            0.0025m,
            DateTimeOffset.UtcNow);

        // Act
        await repository.AddAsync(record);

        // Assert
        var persisted = await _dbContext.CostUsageEntries.FirstOrDefaultAsync(x => x.Id == record.Id);
        Assert.NotNull(persisted);
        Assert.Equal(record.TenantId, persisted!.TenantId);
        Assert.Equal(record.GEMId, persisted.GEMId);
        Assert.Equal(record.Operation, persisted.Operation);
        Assert.Equal(record.TokensUsed, persisted.TokensUsed);
        Assert.Equal(record.Cost, persisted.Cost);
    }

    [Fact]
    public async Task GetMonthlyUsage_ShouldAggregateCorrectly()
    {
        // Arrange
        var repository = new CostUsageRepository(_dbContext);
        var tenantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var periodStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var periodEnd = periodStart.AddMonths(1);

        await repository.AddAsync(new CostUsageRecord(Guid.NewGuid(), tenantId, Guid.NewGuid(), "summarization", 100, 0.001m, periodStart.AddDays(1)));
        await repository.AddAsync(new CostUsageRecord(Guid.NewGuid(), tenantId, Guid.NewGuid(), "tagging", 200, 0.002m, periodStart.AddDays(2)));
        await repository.AddAsync(new CostUsageRecord(Guid.NewGuid(), tenantId, Guid.NewGuid(), "categorization", 300, 0.003m, periodStart.AddMonths(-1).AddDays(2)));

        // Act
        var total = await repository.GetTotalCostAsync(tenantId, periodStart, periodEnd);

        // Assert
        Assert.Equal(0.003m, total);
    }

    [Fact]
    public async Task CostTracking_ShouldIsolateTenants()
    {
        // Arrange
        var repository = new CostUsageRepository(_dbContext);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await repository.AddAsync(new CostUsageRecord(Guid.NewGuid(), tenantA, Guid.NewGuid(), "summarization", 200, 0.010m, now));
        await repository.AddAsync(new CostUsageRecord(Guid.NewGuid(), tenantB, Guid.NewGuid(), "summarization", 400, 0.050m, now));

        // Act
        var tenantATotal = await repository.GetTotalCostAsync(tenantA, now.AddDays(-7), now.AddDays(1));
        var tenantBTotal = await repository.GetTotalCostAsync(tenantB, now.AddDays(-7), now.AddDays(1));

        // Assert
        Assert.Equal(0.010m, tenantATotal);
        Assert.Equal(0.050m, tenantBTotal);
        Assert.NotEqual(tenantATotal, tenantBTotal);
    }

    [Fact]
    public async Task CostReporting_ShouldSupportQueries()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        _dbContext.CostUsageEntries.AddRange(
            new CostUsageEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                GEMId = Guid.NewGuid(),
                Operation = "summarization",
                TokensUsed = 150,
                Cost = 0.0015m,
                CreatedAt = now.AddHours(-12)
            },
            new CostUsageEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                GEMId = Guid.NewGuid(),
                Operation = "tagging",
                TokensUsed = 90,
                Cost = 0.0009m,
                CreatedAt = now.AddHours(-6)
            },
            new CostUsageEntry
            {
                Id = Guid.NewGuid(),
                TenantId = otherTenantId,
                GEMId = Guid.NewGuid(),
                Operation = "summarization",
                TokensUsed = 999,
                Cost = 0.0999m,
                CreatedAt = now.AddHours(-6)
            });

        await _dbContext.SaveChangesAsync();

        // Act
        var report = await _dbContext.CostUsageEntries
            .Where(x => x.TenantId == tenantId && x.CreatedAt >= now.AddDays(-3) && x.CreatedAt < now.AddDays(1))
            .GroupBy(x => x.Operation)
            .Select(group => new
            {
                Operation = group.Key,
                TotalCost = group.Sum(x => x.Cost),
                TotalTokens = group.Sum(x => x.TokensUsed)
            })
            .ToListAsync();

        // Assert
        Assert.Equal(2, report.Count);
        var summarization = report.Single(x => x.Operation == "summarization");
        var tagging = report.Single(x => x.Operation == "tagging");

        Assert.Equal(0.0015m, summarization.TotalCost);
        Assert.Equal(150, summarization.TotalTokens);
        Assert.Equal(0.0009m, tagging.TotalCost);
        Assert.Equal(90, tagging.TotalTokens);
    }
}
