using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Domain.ValueObjects;
using InfoDumpManager.Infrastructure.Repositories;
using Xunit;

namespace InfoDumpManager.Tests.Integration.Infrastructure;

public sealed class GEMRepositoryTests : PostgresIntegrationTestBase
{
    [Fact]
    public async Task AddAsync_ValidGem_PersistsToDatabase()
    {
        if (ShouldSkip)
        {
            Console.WriteLine(SkipReason);
            return;
        }

        await ResetDatabaseAsync();
        await using var context = CreateContext();
        var repository = new GEMRepository(context);
        var unitOfWork = new UnitOfWork(context);
        var gem = GEM.Create(GEMSource.Create("https://example.com/first"), "Integration Gem");

        await repository.AddAsync(gem);
        await unitOfWork.SaveChangesAsync();

        await using var verificationContext = CreateContext();
        var persisted = await verificationContext.Gems.FindAsync(gem.Id);

        persisted.Should().NotBeNull();
        persisted!.Title.Should().Be("Integration Gem");
    }

    [Fact]
    public async Task GetByIdAsync_ExistingGem_ReturnsGem()
    {
        if (ShouldSkip)
        {
            Console.WriteLine(SkipReason);
            return;
        }

        await ResetDatabaseAsync();
        await using var context = CreateContext();
        var repository = new GEMRepository(context);
        var unitOfWork = new UnitOfWork(context);
        var gem = GEM.Create(GEMSource.Create("https://example.com/second"), "Findable Gem");

        await repository.AddAsync(gem);
        await unitOfWork.SaveChangesAsync();

        var retrieved = await repository.GetByIdAsync(gem.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Source.Should().Be(gem.Source);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingGem_ReturnsNull()
    {
        if (ShouldSkip)
        {
            Console.WriteLine(SkipReason);
            return;
        }

        await ResetDatabaseAsync();
        await using var context = CreateContext();
        var repository = new GEMRepository(context);

        var result = await repository.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ExistingGem_SavesChanges()
    {
        if (ShouldSkip)
        {
            Console.WriteLine(SkipReason);
            return;
        }

        await ResetDatabaseAsync();
        await using var context = CreateContext();
        var repository = new GEMRepository(context);
        var unitOfWork = new UnitOfWork(context);
        var gem = GEM.Create(GEMSource.Create("https://example.com/update"), "Old Title");

        await repository.AddAsync(gem);
        await unitOfWork.SaveChangesAsync();

        gem.UpdateTitle("Updated Title");
        await repository.UpdateAsync(gem);
        await unitOfWork.SaveChangesAsync();

        await using var verificationContext = CreateContext();
        var updated = await verificationContext.Gems.FindAsync(gem.Id);

        updated.Should().NotBeNull();
        updated!.Title.Should().Be("Updated Title");
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsPaginatedResponse()
    {
        if (ShouldSkip)
        {
            Console.WriteLine(SkipReason);
            return;
        }

        await ResetDatabaseAsync();
        await using var context = CreateContext();
        var repository = new GEMRepository(context);
        var unitOfWork = new UnitOfWork(context);
        const int pageSize = 5;
        const int total = 12;

        for (var i = 0; i < total; i++)
        {
            var gem = GEM.Create(GEMSource.Create($"https://example.com/{i}"), $"Gem {i}");
            await repository.AddAsync(gem);
        }

        await unitOfWork.SaveChangesAsync();

        var (items, count) = await repository.GetPagedAsync(page: 2, pageSize: pageSize);

        count.Should().Be(total);
        items.Should().HaveCount(pageSize);
        items.All(item => !string.IsNullOrWhiteSpace(item.Title)).Should().BeTrue();
    }
}
