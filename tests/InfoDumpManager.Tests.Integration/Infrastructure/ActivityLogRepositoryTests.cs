using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Enums;
using InfoDumpManager.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoDumpManager.Tests.Integration.Infrastructure;

public sealed class ActivityLogRepositoryTests : PostgresIntegrationTestBase
{
    [Fact]
    public async Task AddAsync_ValidActivityLog_PersistsToDatabase()
    {
        if (ShouldSkip)
        {
            Console.WriteLine(SkipReason);
            return;
        }

        await ResetDatabaseAsync();
        await using var context = CreateContext();
        var repository = new ActivityLogRepository(context);
        var unitOfWork = new UnitOfWork(context);
        var log = ActivityLog.Create(ActivityType.CategoryCreated, "Category created", Guid.NewGuid(), Guid.NewGuid());

        await repository.AddAsync(log);
        await unitOfWork.SaveChangesAsync();

        await using var verificationContext = CreateContext();
        var persisted = await verificationContext.ActivityLogs.FindAsync(log.Id);

        persisted.Should().NotBeNull();
        persisted!.Message.Should().Be("Category created");
    }

    [Fact]
    public async Task GetByGemIdAsync_ReturnsRelatedLogs()
    {
        if (ShouldSkip)
        {
            Console.WriteLine(SkipReason);
            return;
        }

        await ResetDatabaseAsync();
        await using var context = CreateContext();
        var repository = new ActivityLogRepository(context);
        var unitOfWork = new UnitOfWork(context);
        var gemId = Guid.NewGuid();
        var otherGemId = Guid.NewGuid();

        for (var i = 0; i < 3; i++)
        {
            await repository.AddAsync(ActivityLog.Create(ActivityType.GEMUpdated, $"Log {i}", Guid.NewGuid(), gemId));
        }

        await repository.AddAsync(ActivityLog.Create(ActivityType.GEMUpdated, "Other", Guid.NewGuid(), otherGemId));
        await unitOfWork.SaveChangesAsync();

        await using var verificationContext = CreateContext();
        var relatedLogs = await verificationContext.ActivityLogs
            .Where(x => x.GemId == gemId)
            .ToListAsync();

        relatedLogs.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetByDateRangeAsync_ReturnsLogsInRange()
    {
        if (ShouldSkip)
        {
            Console.WriteLine(SkipReason);
            return;
        }

        await ResetDatabaseAsync();
        await using var context = CreateContext();
        var repository = new ActivityLogRepository(context);
        var unitOfWork = new UnitOfWork(context);
        var recentLog = ActivityLog.Create(ActivityType.SnapshotStored, "Recent", Guid.NewGuid(), Guid.NewGuid());
        var oldLog = ActivityLog.Create(ActivityType.SnapshotStored, "Old", Guid.NewGuid(), Guid.NewGuid());

        await repository.AddAsync(oldLog);
        await repository.AddAsync(recentLog);
        await unitOfWork.SaveChangesAsync();

        var oldTimestamp = DateTime.UtcNow.AddDays(-1);
        await using var updateContext = CreateContext();
        await updateContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE activity_logs SET created_at_utc = {oldTimestamp} WHERE id = {oldLog.Id}");

        await using var verificationContext = CreateContext();
        var windowStart = DateTime.UtcNow.AddMinutes(-5);
        var windowEnd = DateTime.UtcNow.AddMinutes(5);
        var logs = await verificationContext.ActivityLogs
            .Where(x => x.CreatedAtUtc >= windowStart && x.CreatedAtUtc <= windowEnd)
            .ToListAsync();

        logs.Should().ContainSingle(x => x.Id == recentLog.Id);
    }
}
