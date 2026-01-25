using System;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Enums;
using Xunit;

namespace InfoDumpManager.Tests.Unit.Domain;

public class ActivityLogTests
{
    [Fact]
    public void Create_ValidInput_SetsProperties()
    {
        var userId = Guid.NewGuid();
        var gemId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var log = ActivityLog.Create(
            ActivityType.GEMCreated,
            "  Created a new GEM  ",
            userId,
            gemId,
            categoryId);

        Assert.Equal(ActivityType.GEMCreated, log.ActivityType);
        Assert.Equal("Created a new GEM", log.Message);
        Assert.Equal(userId, log.UserId);
        Assert.Equal(gemId, log.GemId);
        Assert.Equal(categoryId, log.CategoryId);
    }

    [Fact]
    public void Create_EmptyMessage_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            ActivityLog.Create(ActivityType.SnapshotStored, "   "));
    }
}
