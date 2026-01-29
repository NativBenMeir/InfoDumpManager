using System;
using FluentAssertions;
using InfoDumpManager.Domain.Entities;
using Xunit;

namespace InfoDumpManager.Tests.Unit;

public sealed class CategoryEntityTests
{
    [Fact]
    public void Create_WithWhitespaceName_TrimsValues()
    {
        var tenantId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();

        var category = Category.Create(tenantId, "  Example Category  ", createdBy, " Description ");

        category.Name.Should().Be("Example Category");
        category.Description.Should().Be("Description");
        category.CreatedById.Should().Be(createdBy);
    }

    [Fact]
    public void Create_WithEmptyName_ThrowsArgumentException()
    {
        var tenantId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();

        Action act = () => Category.Create(tenantId, "   ", createdBy);

        act.Should().Throw<ArgumentException>().WithMessage("*name*");
    }

    [Fact]
    public void UpdateName_WithValidName_UpdatesNameAndTimestamp()
    {
        var tenantId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var category = Category.Create(tenantId, "Original Name", createdBy);
        var beforeUpdate = DateTimeOffset.UtcNow;

        category.UpdateName("  New Name  ");

        category.Name.Should().Be("New Name");
        category.UpdatedAt.Should().NotBeNull();
        category.UpdatedAt.Should().BeOnOrAfter(beforeUpdate);
    }

    [Fact]
    public void UpdateDescription_WithValidDescription_UpdatesDescriptionAndTimestamp()
    {
        var tenantId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var category = Category.Create(tenantId, "Name", createdBy, "Original Description");
        var beforeUpdate = DateTimeOffset.UtcNow;

        category.UpdateDescription("  New Description  ");

        category.Description.Should().Be("New Description");
        category.UpdatedAt.Should().NotBeNull();
        category.UpdatedAt.Should().BeOnOrAfter(beforeUpdate);
    }

    [Fact]
    public void AddGem_WithGemFromDifferentTenant_ThrowsInvalidOperationException()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var category = Category.Create(tenantId, "Category", createdBy);
        var source = new InfoDumpManager.Domain.ValueObjects.GEMSource("https://example.com");
        var snapshot = new InfoDumpManager.Domain.ValueObjects.GEMSnapshot("<html></html>");
        var gem = GEM.Create(otherTenantId, "GEM", "https://example.com/page", source, snapshot);

        Action act = () => category.AddGem(gem);

        act.Should().Throw<InvalidOperationException>().WithMessage("*another tenant*");
    }

    [Fact]
    public void AddGem_WithDuplicateGem_DoesNotAddAgain()
    {
        var tenantId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var category = Category.Create(tenantId, "Category", createdBy);
        var source = new InfoDumpManager.Domain.ValueObjects.GEMSource("https://example.com");
        var snapshot = new InfoDumpManager.Domain.ValueObjects.GEMSnapshot("<html></html>");
        var gem = GEM.Create(tenantId, "GEM", "https://example.com/page", source, snapshot);

        category.AddGem(gem);
        var countAfterFirst = category.Gems.Count;
        category.AddGem(gem);

        category.Gems.Count.Should().Be(countAfterFirst);
    }

    [Fact]
    public void Create_WithNameExceeding128Characters_ThrowsArgumentException()
    {
        var tenantId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var longName = new string('a', 130);

        Action act = () => Category.Create(tenantId, longName, createdBy);

        act.Should().Throw<ArgumentException>().WithMessage("*128*");
    }

    [Fact]
    public void Create_WithDescriptionExceeding512Characters_ThrowsArgumentException()
    {
        var tenantId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var longDescription = new string('a', 520);

        Action act = () => Category.Create(tenantId, "Name", createdBy, longDescription);

        act.Should().Throw<ArgumentException>().WithMessage("*512*");
    }
}
