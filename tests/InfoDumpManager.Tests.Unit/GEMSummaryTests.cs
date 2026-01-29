using System;
using FluentAssertions;
using InfoDumpManager.Domain.ValueObjects;
using Xunit;

namespace InfoDumpManager.Tests.Unit;

public sealed class GEMSummaryTests
{
    [Fact]
    public void Create_WithNegativeTokenCount_ThrowsArgumentOutOfRangeException()
    {
        Action act = () => GEMSummary.Create("Summary", "gpt-4", -1, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*token*");
    }

    [Fact]
    public void Empty_ReturnsEmptyInstance()
    {
        var empty = GEMSummary.Empty;

        empty.Text.Should().BeEmpty();
        empty.Model.Should().BeEmpty();
        empty.TokenCount.Should().Be(0);
        empty.GeneratedAt.Should().Be(DateTimeOffset.MinValue);
    }

    [Fact]
    public void InstancesWithSameValues_AreEqual()
    {
        var generatedAt = DateTimeOffset.UtcNow;
        var first = GEMSummary.Create("Summary text", "gpt-4", 42, generatedAt);
        var second = GEMSummary.Create("Summary text", "gpt-4", 42, generatedAt);

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }
}
