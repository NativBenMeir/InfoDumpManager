using System;
using System.Linq;
using InfoDumpManager.Domain.Entities;
using Xunit;

namespace InfoDumpManager.Tests.Unit.Domain;

public class CategoryTests
{
    [Fact]
    public void Create_TrimsNameAndDescription()
    {
        var category = Category.Create("  Research  ", "  AI lab  ");

        Assert.Equal("Research", category.Name);
        Assert.Equal("AI lab", category.Description);
    }

    [Fact]
    public void Rename_EmptyName_ThrowsArgumentException()
    {
        var category = Category.Create("Science");

        Assert.Throws<ArgumentException>(() => category.Rename("  "));
    }

    [Fact]
    public void AssignGem_DoesNotDuplicateEntries()
    {
        var category = Category.Create("News");
        var gemId = Guid.NewGuid();

        category.AssignGem(gemId);
        category.AssignGem(gemId);

        Assert.Single(category.GemIds);
        Assert.Equal(gemId, category.GemIds.First());
    }

    [Fact]
    public void RemoveGem_RemovesExistingGemId()
    {
        var category = Category.Create("Updates");
        var gemId = Guid.NewGuid();

        category.AssignGem(gemId);
        category.RemoveGem(gemId);

        Assert.Empty(category.GemIds);
    }

    [Fact]
    public void UpdateDescription_TrimsAndClearsBlank()
    {
        var category = Category.Create("Product", "Machine Learning");

        category.UpdateDescription("  New description  ");
        Assert.Equal("New description", category.Description);

        category.UpdateDescription("   ");
        Assert.Null(category.Description);
    }
}
