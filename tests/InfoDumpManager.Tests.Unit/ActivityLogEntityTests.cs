using System;
using System.Text.Json;
using FluentAssertions;
using InfoDumpManager.Domain.Entities;
using Xunit;

namespace InfoDumpManager.Tests.Unit;

public sealed class ActivityLogEntityTests
{
    [Fact]
    public void Create_WithValidData_PopulatesProperties()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var metadata = JsonDocument.Parse("{\"key\":\"value\"}");

        var log = ActivityLog.Create(
            tenantId,
            ActivityEventType.GEMCreated,
            "GEM",
            "GEM created successfully",
            entityId,
            userId,
            metadata);

        log.TenantId.Should().Be(tenantId);
        log.EventType.Should().Be(ActivityEventType.GEMCreated);
        log.EntityName.Should().Be("GEM");
        log.Description.Should().Be("GEM created successfully");
        log.EntityId.Should().Be(entityId);
        log.UserId.Should().Be(userId);
        log.Metadata.Should().NotBeNull();
        log.OccurredAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_WithEmptyTenantId_ThrowsArgumentException()
    {
        Action act = () => ActivityLog.Create(
            Guid.Empty,
            ActivityEventType.GEMCreated,
            "GEM",
            "Description");

        act.Should().Throw<ArgumentException>().WithMessage("*Tenant*");
    }

    [Fact]
    public void Create_WithEmptyEntityName_ThrowsArgumentException()
    {
        var tenantId = Guid.NewGuid();

        Action act = () => ActivityLog.Create(
            tenantId,
            ActivityEventType.GEMCreated,
            "   ",
            "Description");

        act.Should().Throw<ArgumentException>().WithMessage("*Entity name*");
    }

    [Fact]
    public void Create_WithEmptyDescription_ThrowsArgumentException()
    {
        var tenantId = Guid.NewGuid();

        Action act = () => ActivityLog.Create(
            tenantId,
            ActivityEventType.GEMCreated,
            "GEM",
            "   ");

        act.Should().Throw<ArgumentException>().WithMessage("*Description*");
    }

    [Fact]
    public void Create_WithMetadata_StoresMetadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = JsonDocument.Parse("{\"action\":\"test\",\"count\":5}");

        var log = ActivityLog.Create(
            tenantId,
            ActivityEventType.GEMCreated,
            "Category",
            "Category created",
            metadata: metadata);

        log.Metadata.Should().NotBeNull();
        log.Metadata!.RootElement.GetProperty("action").GetString().Should().Be("test");
        log.Metadata.RootElement.GetProperty("count").GetInt32().Should().Be(5);
    }
}
