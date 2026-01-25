using System;
using System.Linq;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.ValueObjects;
using Xunit;

namespace InfoDumpManager.Tests.Unit.Domain;

public class GemTests
{
    [Fact]
    public void Create_WithWhitespaceTitle_ThrowsArgumentException()
    {
        var source = GEMSource.Create("https://example.com");

        Assert.Throws<ArgumentException>(() => GEM.Create(source, "   "));
    }

    [Fact]
    public void Create_ValidInput_TrimsTitleAndAssignsSource()
    {
        var source = GEMSource.Create("https://example.com");
        var gem = GEM.Create(source, "  Trimmed Title  ");

        Assert.Equal("Trimmed Title", gem.Title);
        Assert.Same(source, gem.Source);
    }

    [Fact]
    public void UpdateTitle_WithWhitespace_ThrowsArgumentException()
    {
        var gem = GEM.Create(GEMSource.Create("https://example.com"), "Title");

        Assert.Throws<ArgumentException>(() => gem.UpdateTitle("   "));
    }

    [Fact]
    public void UpdateTitle_TrimsValue()
    {
        var gem = GEM.Create(GEMSource.Create("https://example.com"), "Title");

        gem.UpdateTitle("  Updated Title  ");

        Assert.Equal("Updated Title", gem.Title);
    }

    [Fact]
    public void AttachSnapshot_Null_Throws()
    {
        var gem = GEM.Create(GEMSource.Create("https://example.com"), "Title");

        Assert.Throws<ArgumentNullException>(() => gem.AttachSnapshot(null!));
    }

    [Fact]
    public void AttachSnapshot_SetsSnapshot()
    {
        var gem = GEM.Create(GEMSource.Create("https://example.com"), "Title");
        var snapshot = GEMSnapshot.Create("<html></html>", "text/html", DateTime.UtcNow);

        gem.AttachSnapshot(snapshot);

        Assert.Same(snapshot, gem.Snapshot);
    }

    [Fact]
    public void SetSummary_Null_Throws()
    {
        var gem = GEM.Create(GEMSource.Create("https://example.com"), "Title");

        Assert.Throws<ArgumentNullException>(() => gem.SetSummary(null!));
    }

    [Fact]
    public void SetSummary_SetsValue()
    {
        var gem = GEM.Create(GEMSource.Create("https://example.com"), "Title");
        var summary = GEMSummary.Create("Summary text");

        gem.SetSummary(summary);

        Assert.Same(summary, gem.Summary);
    }

    [Fact]
    public void AssignCategory_AddsOnlyUniqueIds()
    {
        var source = GEMSource.Create("https://example.com");
        var gem = GEM.Create(source, "Title");
        var categoryId = Guid.NewGuid();

        gem.AssignCategory(categoryId);
        gem.AssignCategory(categoryId);

        var categories = gem.CategoryIds.ToArray();
        Assert.Single(categories);
        Assert.Equal(categoryId, categories[0]);
    }
}

public class GemSourceTests
{
    [Fact]
    public void Create_WithEmptyUrl_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => GEMSource.Create(string.Empty));
    }

    [Fact]
    public void Create_WithInvalidUrl_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => GEMSource.Create("not-a-url"));
    }

    [Fact]
    public void Create_WithValidUrl_ReturnsSameValue()
    {
        var source = GEMSource.Create("https://example.com/path");

        Assert.Equal("https://example.com/path", source.Url);
    }
}

public class GEMSnapshotTests
{
    [Fact]
    public void Create_ValidInput_SetsProperties()
    {
        var retrievedAt = DateTime.UtcNow;
        var snapshot = GEMSnapshot.Create("<html></html>", "text/html", retrievedAt);

        Assert.Equal("<html></html>", snapshot.Content);
        Assert.Equal("text/html", snapshot.ContentType);
        Assert.Equal(retrievedAt, snapshot.RetrievedAtUtc);
    }

    [Fact]
    public void Create_EmptyContent_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => GEMSnapshot.Create(string.Empty, "text/html", DateTime.UtcNow));
    }
}

public class GEMSummaryTests
{
    [Fact]
    public void Create_ValidInput_SetsText()
    {
        var summary = GEMSummary.Create("Short summary");

        Assert.Equal("Short summary", summary.Text);
        Assert.NotEqual(default, summary.GeneratedAtUtc);
    }

    [Fact]
    public void Create_EmptyText_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => GEMSummary.Create(string.Empty));
    }
}
