using System;
using FluentAssertions;
using InfoDumpManager.Domain.ValueObjects;
using Xunit;

namespace InfoDumpManager.Tests.Unit;

public sealed class GEMSnapshotTests
{
    [Fact]
    public void Create_WithEmptyHtmlContent_ThrowsArgumentException()
    {
        Action act = () => new GEMSnapshot("   ");

        act.Should().Throw<ArgumentException>().WithMessage("*content*");
    }

    [Fact]
    public void InstancesWithSameValues_AreEqual()
    {
        var capturedAt = DateTimeOffset.UtcNow;
        var first = new GEMSnapshot("<html><body>Content</body></html>", "text/html", capturedAt);
        var second = new GEMSnapshot("<html><body>Content</body></html>", "text/html", capturedAt);

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }
}
