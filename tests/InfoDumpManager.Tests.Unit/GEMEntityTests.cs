using System;
using FluentAssertions;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.ValueObjects;
using Xunit;

namespace InfoDumpManager.Tests.Unit;

public sealed class GEMEntityTests
{
    [Fact]
    public void Create_WithValidData_PopulatesProperties()
    {
        var tenantId = Guid.NewGuid();
        var source = new GEMSource("https://example.com", "Example Source");
        var snapshot = new GEMSnapshot("<html></html>");
        var summary = GEMSummary.Create("Summary", "gpt-4", 42, DateTimeOffset.UtcNow);

        var gem = GEM.Create(tenantId, "  Example GEM ", "https://example.com/page", source, snapshot, summary);

        gem.TenantId.Should().Be(tenantId);
        gem.Title.Should().Be("Example GEM");
        gem.Url.Should().Be("https://example.com/page");
        gem.Source.Should().Be(source);
        gem.Snapshot.Should().Be(snapshot);
        gem.Summary.Should().Be(summary);
        gem.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        gem.IsDeleted.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("ftp://example.com")]
    public void Create_WithInvalidUrl_ThrowsArgumentException(string url)
    {
        var tenantId = Guid.NewGuid();
        var source = new GEMSource("https://example.com");
        var snapshot = new GEMSnapshot("<html></html>");

        Action act = () => GEM.Create(tenantId, "Title", url, source, snapshot);

        act.Should().Throw<ArgumentException>().WithMessage("*URL*");
    }

    [Fact]
    public void Create_WithEmptyTitle_ThrowsArgumentException()
    {
        var tenantId = Guid.NewGuid();
        var source = new GEMSource("https://example.com");
        var snapshot = new GEMSnapshot("<html></html>");

        Action act = () => GEM.Create(tenantId, "   ", "https://example.com/page", source, snapshot);

        act.Should().Throw<ArgumentException>().WithMessage("*Title*");
    }

    [Fact]
    public void AssignCategory_WhenCategoryFromDifferentTenant_ThrowsInvalidOperationException()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var source = new GEMSource("https://example.com");
        var snapshot = new GEMSnapshot("<html></html>");
        var gem = GEM.Create(tenantId, "Title", "https://example.com/page", source, snapshot);
        var category = Category.Create(otherTenantId, "Category", Guid.NewGuid());

        Action act = () => gem.AssignCategory(category);

        act.Should().Throw<InvalidOperationException>().WithMessage("*another tenant*");
    }

    [Fact]
    public void UpdateSummary_WithNullSummary_ThrowsArgumentNullException()
    {
        var tenantId = Guid.NewGuid();
        var source = new GEMSource("https://example.com");
        var snapshot = new GEMSnapshot("<html></html>");
        var gem = GEM.Create(tenantId, "Title", "https://example.com/page", source, snapshot);

        Action act = () => gem.UpdateSummary(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MarkAsDeleted_SetsIsDeletedAndUpdatesTimestamp()
    {
        var tenantId = Guid.NewGuid();
        var source = new GEMSource("https://example.com");
        var snapshot = new GEMSnapshot("<html></html>");
        var gem = GEM.Create(tenantId, "Title", "https://example.com/page", source, snapshot);
        var beforeDelete = DateTimeOffset.UtcNow;

        gem.MarkAsDeleted();

        gem.IsDeleted.Should().BeTrue();
        gem.UpdatedAt.Should().NotBeNull();
        gem.UpdatedAt.Should().BeOnOrAfter(beforeDelete);
    }

    [Fact]
    public void UpdateTitle_WithValidTitle_UpdatesTitleAndTimestamp()
    {
        var tenantId = Guid.NewGuid();
        var source = new GEMSource("https://example.com");
        var snapshot = new GEMSnapshot("<html></html>");
        var gem = GEM.Create(tenantId, "Original Title", "https://example.com/page", source, snapshot);
        var beforeUpdate = DateTimeOffset.UtcNow;

        gem.UpdateTitle("  New Title  ");

        gem.Title.Should().Be("New Title");
        gem.UpdatedAt.Should().NotBeNull();
        gem.UpdatedAt.Should().BeOnOrAfter(beforeUpdate);
    }

    [Fact]
    public void Create_WithUrlExceeding2048Characters_ThrowsArgumentException()
    {
        var tenantId = Guid.NewGuid();
        var source = new GEMSource("https://example.com");
        var snapshot = new GEMSnapshot("<html></html>");
        var longUrl = "https://example.com/" + new string('a', 2050);

        Action act = () => GEM.Create(tenantId, "Title", longUrl, source, snapshot);

        act.Should().Throw<ArgumentException>().WithMessage("*2048*");
    }

    [Fact]
    public void Create_WithTitleExceeding256Characters_ThrowsArgumentException()
    {
        var tenantId = Guid.NewGuid();
        var source = new GEMSource("https://example.com");
        var snapshot = new GEMSnapshot("<html></html>");
        var longTitle = new string('a', 260);

        Action act = () => GEM.Create(tenantId, longTitle, "https://example.com/page", source, snapshot);

        act.Should().Throw<ArgumentException>().WithMessage("*256*");
    }
}
