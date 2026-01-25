using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Infrastructure.Repositories;
using Xunit;

namespace InfoDumpManager.Tests.Integration.Infrastructure;

public sealed class CategoryRepositoryTests : PostgresIntegrationTestBase
{
    [Fact]
    public async Task AddAsync_ValidCategory_PersistsToDatabase()
    {
        if (ShouldSkip)
        {
            Console.WriteLine(SkipReason);
            return;
        }

        await ResetDatabaseAsync();
        await using var context = CreateContext();
        var repository = new CategoryRepository(context);
        var unitOfWork = new UnitOfWork(context);
        var category = Category.Create("Data", "Related info");

        await repository.AddAsync(category);
        await unitOfWork.SaveChangesAsync();

        await using var verificationContext = CreateContext();
        var persisted = await verificationContext.Categories.FindAsync(category.Id);

        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be("Data");
        persisted.Description.Should().Be("Related info");
    }

    [Fact]
    public async Task GetByIdAsync_ExistingCategory_ReturnsCategory()
    {
        if (ShouldSkip)
        {
            Console.WriteLine(SkipReason);
            return;
        }

        await ResetDatabaseAsync();
        await using var context = CreateContext();
        var repository = new CategoryRepository(context);
        var unitOfWork = new UnitOfWork(context);
        var category = Category.Create("Science");

        await repository.AddAsync(category);
        await unitOfWork.SaveChangesAsync();

        var retrieved = await repository.GetByIdAsync(category.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Science");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllCategories()
    {
        if (ShouldSkip)
        {
            Console.WriteLine(SkipReason);
            return;
        }

        await ResetDatabaseAsync();
        await using var context = CreateContext();
        var repository = new CategoryRepository(context);
        var unitOfWork = new UnitOfWork(context);
        var names = new[] { "Alpha", "Beta", "Gamma", "Delta", "Epsilon" };

        foreach (var name in names)
        {
            await repository.AddAsync(Category.Create(name));
        }

        await unitOfWork.SaveChangesAsync();

        var categories = await repository.GetAllAsync();

        categories.Should().HaveCount(names.Length);
        categories.Select(x => x.Name).Should().BeEquivalentTo(names);
    }

    [Fact]
    public async Task DeleteAsync_ExistingCategory_RemovesFromDatabase()
    {
        if (ShouldSkip)
        {
            Console.WriteLine(SkipReason);
            return;
        }

        await ResetDatabaseAsync();
        await using var context = CreateContext();
        var repository = new CategoryRepository(context);
        var unitOfWork = new UnitOfWork(context);
        var category = Category.Create("Temporary");

        await repository.AddAsync(category);
        await unitOfWork.SaveChangesAsync();

        await repository.DeleteAsync(category);
        await unitOfWork.SaveChangesAsync();

        await using var verificationContext = CreateContext();
        var deleted = await verificationContext.Categories.FindAsync(category.Id);

        deleted.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ExistingCategory_SavesChanges()
    {
        if (ShouldSkip)
        {
            Console.WriteLine(SkipReason);
            return;
        }

        await ResetDatabaseAsync();
        await using var context = CreateContext();
        var repository = new CategoryRepository(context);
        var unitOfWork = new UnitOfWork(context);
        var category = Category.Create("Initial", "Original desc");

        await repository.AddAsync(category);
        await unitOfWork.SaveChangesAsync();

        category.Rename("Revised");
        category.UpdateDescription("Updated desc");
        await repository.UpdateAsync(category);
        await unitOfWork.SaveChangesAsync();

        await using var verificationContext = CreateContext();
        var updated = await verificationContext.Categories.FindAsync(category.Id);

        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Revised");
        updated.Description.Should().Be("Updated desc");
    }
}
