using System;
using InfoDumpManager.Domain.Entities;
using Xunit;

namespace InfoDumpManager.Tests.Unit.Domain;

public class UserTests
{
    [Fact]
    public void Create_ValidInput_TrimsValues()
    {
        var user = User.Create("  user@example.com  ", "  Display Name  ");

        Assert.Equal("user@example.com", user.Email);
        Assert.Equal("Display Name", user.DisplayName);
    }

    [Fact]
    public void Create_EmptyEmail_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => User.Create("   ", "Name"));
    }

    [Fact]
    public void Create_EmptyDisplayName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => User.Create("user@example.com", "   "));
    }

    [Fact]
    public void UpdateDisplayName_TrimsValue()
    {
        var user = User.Create("user@example.com", "Display");

        user.UpdateDisplayName("  New Name  ");

        Assert.Equal("New Name", user.DisplayName);
    }

    [Fact]
    public void UpdateDisplayName_EmptyValue_ThrowsArgumentException()
    {
        var user = User.Create("user@example.com", "Display");

        Assert.Throws<ArgumentException>(() => user.UpdateDisplayName("   "));
    }
}
