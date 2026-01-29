using FluentAssertions;
using InfoDumpManager.Domain.ValueObjects;
using Xunit;

namespace InfoDumpManager.Tests.Unit;

public sealed class GEMSourceTests
{
    [Fact]
    public void InstancesWithSameValues_AreEqual()
    {
        var first = new GEMSource("https://example.com", "Source Title");
        var second = new GEMSource("https://example.com", "Source Title");

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void Create_WithNonAbsoluteUrl_ThrowsArgumentException()
    {
        Action act = () => new GEMSource("/relative/path");

        act.Should().Throw<ArgumentException>().WithMessage("*absolute*");
    }
}
