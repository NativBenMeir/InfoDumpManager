using System;
using System.Threading.Tasks;
using FluentAssertions;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.ValueObjects;
using InfoDumpManager.Infrastructure.Repositories;
using InfoDumpManager.Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoDumpManager.Tests.Integration;

[Collection("IntegrationTests")]
public sealed class RepositoryIntegrationTests
{
    private readonly PostgresTestcontainerFixture _fixture;

    public RepositoryIntegrationTests(PostgresTestcontainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GEMRepository_CanInsertAndRetrieveGem()
    {
        var tenantId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var category = Category.Create(tenantId, "GEM Repo Category", createdBy);
        var source = new GEMSource("https://example.com", "Example");
        var snapshot = new GEMSnapshot("<html><body>Snapshot</body></html>");
        var gem = GEM.Create(tenantId, "Repository GEM", "https://example.com/page", source, snapshot);
        category.AddGem(gem);

        await using (var context = _fixture.CreateContext())
        await using (var unitOfWork = new UnitOfWork(context))
        {
            await unitOfWork.Categories.AddAsync(category);
            await unitOfWork.GEMs.AddAsync(gem);
            await unitOfWork.SaveChangesAsync();
        }

        await using (var verifyContext = _fixture.CreateContext())
        {
            var gemRepository = new GEMRepository(verifyContext);
            var loaded = await gemRepository.GetByIdAsync(gem.Id);

            loaded.Should().NotBeNull();
            loaded!.Title.Should().Be(gem.Title);
            loaded.Url.Should().Be(gem.Url);
            loaded.Category.Should().NotBeNull();
            loaded.Category!.Name.Should().Be(category.Name);
        }
    }

    [Fact]
    public async Task CategoryRepository_CanQueryByName()
    {
        var tenantId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var categoryName = "Repository Category";
        var category = Category.Create(tenantId, categoryName, createdBy, "Integration description");

        await using (var context = _fixture.CreateContext())
        await using (var unitOfWork = new UnitOfWork(context))
        {
            await unitOfWork.Categories.AddAsync(category);
            await unitOfWork.SaveChangesAsync();
        }

        await using (var verifyContext = _fixture.CreateContext())
        {
            var categoryRepository = new CategoryRepository(verifyContext);
            var loaded = await categoryRepository.GetByNameAsync(tenantId, categoryName);

            loaded.Should().NotBeNull();
            loaded!.Description.Should().Be("Integration description");
        }
    }

    [Fact]
    public async Task UnitOfWork_RollbackOccursWhenConstraintViolationHappens()
    {
        var tenantId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var categoryName = "Duplicate Category";
        var first = Category.Create(tenantId, categoryName, createdBy);
        var second = Category.Create(tenantId, categoryName, createdBy);

        await using (var context = _fixture.CreateContext())
        await using (var unitOfWork = new UnitOfWork(context))
        {
            await unitOfWork.Categories.AddAsync(first);
            await unitOfWork.Categories.AddAsync(second);

            Func<Task> saving = () => unitOfWork.SaveChangesAsync();
            await saving.Should().ThrowAsync<DbUpdateException>();
        }

        await using (var verifyContext = _fixture.CreateContext())
        {
            var count = await verifyContext.Categories.CountAsync(x => x.TenantId == tenantId);
            count.Should().Be(0);
        }
    }

    [Fact]
    public async Task GEMRepository_GetByUrlAsync_ReturnsCorrectGem()
    {
        var tenantId = Guid.NewGuid();
        var url = "https://example.com/unique-page";
        var source = new GEMSource(url);
        var snapshot = new GEMSnapshot("<html></html>");
        var gem = GEM.Create(tenantId, "Unique GEM", url, source, snapshot);

        await using (var context = _fixture.CreateContext())
        await using (var unitOfWork = new UnitOfWork(context))
        {
            await unitOfWork.GEMs.AddAsync(gem);
            await unitOfWork.SaveChangesAsync();
        }

        await using (var verifyContext = _fixture.CreateContext())
        {
            var gemRepository = new GEMRepository(verifyContext);
            var loaded = await gemRepository.GetByUrlAsync(tenantId, url);

            loaded.Should().NotBeNull();
            loaded!.Title.Should().Be("Unique GEM");
        }
    }

    [Fact]
    public async Task GEMRepository_ListByTenantAsync_ReturnsOnlyTenantGems()
    {
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();
        var source = new GEMSource("https://example.com");
        var snapshot = new GEMSnapshot("<html></html>");
        var gem1 = GEM.Create(tenant1, "Tenant1 GEM", "https://example.com/page1", source, snapshot);
        var gem2 = GEM.Create(tenant2, "Tenant2 GEM", "https://example.com/page2", source, snapshot);

        await using (var context = _fixture.CreateContext())
        await using (var unitOfWork = new UnitOfWork(context))
        {
            await unitOfWork.GEMs.AddAsync(gem1);
            await unitOfWork.GEMs.AddAsync(gem2);
            await unitOfWork.SaveChangesAsync();
        }

        await using (var verifyContext = _fixture.CreateContext())
        {
            var gemRepository = new GEMRepository(verifyContext);
            var tenant1Gems = await gemRepository.ListByTenantAsync(tenant1);

            tenant1Gems.Should().HaveCount(1);
            tenant1Gems.Should().Contain(g => g.Title == "Tenant1 GEM");
            tenant1Gems.Should().NotContain(g => g.Title == "Tenant2 GEM");
        }
    }

    [Fact]
    public async Task GEMRepository_ListByCategoryAsync_ReturnsGemsInCategory()
    {
        var tenantId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var category = Category.Create(tenantId, "Test Category", createdBy);
        var source = new GEMSource("https://example.com", "test");
        var snapshot = new GEMSnapshot("<html></html>");
        var gem1 = GEM.Create(tenantId, "GEM1", "https://example.com/page1", source, snapshot);
        var gem2 = GEM.Create(tenantId, "GEM2", "https://example.com/page2", source, snapshot);
        category.AddGem(gem1);
        category.AddGem(gem2);

        await using (var context = _fixture.CreateContext())
        await using (var unitOfWork = new UnitOfWork(context))
        {
            await unitOfWork.Categories.AddAsync(category);
            await unitOfWork.GEMs.AddAsync(gem1);
            await unitOfWork.GEMs.AddAsync(gem2);
            await unitOfWork.SaveChangesAsync();
        }

        await using (var verifyContext = _fixture.CreateContext())
        {
            var gemRepository = new GEMRepository(verifyContext);
            var categoryGems = await gemRepository.ListByCategoryAsync(category.Id);

            categoryGems.Should().HaveCount(2);
            categoryGems.Should().Contain(g => g.Title == "GEM1");
            categoryGems.Should().Contain(g => g.Title == "GEM2");
        }
    }

    [Fact]
    public async Task GEMRepository_ExistsByUrlAsync_ReturnsTrueWhenExists()
    {
        var tenantId = Guid.NewGuid();
        var url = "https://example.com/exists";
        var source = new GEMSource(url);
        var snapshot = new GEMSnapshot("<html></html>");
        var gem = GEM.Create(tenantId, "Existing GEM", url, source, snapshot);

        await using (var context = _fixture.CreateContext())
        await using (var unitOfWork = new UnitOfWork(context))
        {
            await unitOfWork.GEMs.AddAsync(gem);
            await unitOfWork.SaveChangesAsync();
        }

        await using (var verifyContext = _fixture.CreateContext())
        {
            var gemRepository = new GEMRepository(verifyContext);
            var exists = await gemRepository.ExistsByUrlAsync(tenantId, url);
            var notExists = await gemRepository.ExistsByUrlAsync(tenantId, "https://example.com/notexists");

            exists.Should().BeTrue();
            notExists.Should().BeFalse();
        }
    }

    [Fact]
    public async Task CategoryRepository_ExistsByNameAsync_ReturnsTrueWhenExists()
    {
        var tenantId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var categoryName = "Existing Category";
        var category = Category.Create(tenantId, categoryName, createdBy);

        await using (var context = _fixture.CreateContext())
        await using (var unitOfWork = new UnitOfWork(context))
        {
            await unitOfWork.Categories.AddAsync(category);
            await unitOfWork.SaveChangesAsync();
        }

        await using (var verifyContext = _fixture.CreateContext())
        {
            var categoryRepository = new CategoryRepository(verifyContext);
            var exists = await categoryRepository.ExistsByNameAsync(tenantId, categoryName);
            var notExists = await categoryRepository.ExistsByNameAsync(tenantId, "Nonexistent Category");

            exists.Should().BeTrue();
            notExists.Should().BeFalse();
        }
    }

    [Fact]
    public async Task ActivityLogRepository_ListByTenantAsync_OrdersByOccurredAtDescending()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        
        // Create logs with slight delay to ensure different timestamps
        var log1 = ActivityLog.Create(tenantId, ActivityEventType.GEMCreated, "GEM", "First log", userId: userId);
        await Task.Delay(10);
        var log2 = ActivityLog.Create(tenantId, ActivityEventType.CategoryCreated, "Category", "Second log", userId: userId);
        await Task.Delay(10);
        var log3 = ActivityLog.Create(tenantId, ActivityEventType.GEMUpdated, "GEM", "Third log", userId: userId);

        await using (var context = _fixture.CreateContext())
        await using (var unitOfWork = new UnitOfWork(context))
        {
            await unitOfWork.ActivityLogs.AddAsync(log1);
            await unitOfWork.ActivityLogs.AddAsync(log2);
            await unitOfWork.ActivityLogs.AddAsync(log3);
            await unitOfWork.SaveChangesAsync();
        }

        await using (var verifyContext = _fixture.CreateContext())
        {
            var activityLogRepository = new ActivityLogRepository(verifyContext);
            var logs = await activityLogRepository.ListByTenantAsync(tenantId);

            logs.Should().HaveCount(3);
            logs.Should().BeInDescendingOrder(l => l.OccurredAt);
            logs.First().Description.Should().Be("Third log");
        }
    }

    [Fact]
    public async Task GEMRepository_DefensiveCopy_AllowsSameValueObjectInstanceForMultipleGEMs()
    {
        var tenantId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var category = Category.Create(tenantId, "Defensive Copy Test", createdBy);
        
        // Intentionally use the same source and snapshot instances for both GEMs
        var sharedSource = new GEMSource("https://example.com", "shared");
        var sharedSnapshot = new GEMSnapshot("<html>shared</html>");
        
        var gem1 = GEM.Create(tenantId, "GEM1", "https://example.com/page1", sharedSource, sharedSnapshot);
        var gem2 = GEM.Create(tenantId, "GEM2", "https://example.com/page2", sharedSource, sharedSnapshot);
        category.AddGem(gem1);
        category.AddGem(gem2);

        await using (var context = _fixture.CreateContext())
        await using (var unitOfWork = new UnitOfWork(context))
        {
            await unitOfWork.Categories.AddAsync(category);
            await unitOfWork.GEMs.AddAsync(gem1);
            await unitOfWork.GEMs.AddAsync(gem2);
            await unitOfWork.SaveChangesAsync();
        }

        await using (var verifyContext = _fixture.CreateContext())
        {
            var gemRepository = new GEMRepository(verifyContext);
            var categoryGems = await gemRepository.ListByCategoryAsync(category.Id);

            categoryGems.Should().HaveCount(2);
            categoryGems.Should().Contain(g => g.Title == "GEM1");
            categoryGems.Should().Contain(g => g.Title == "GEM2");
            categoryGems.Should().OnlyContain(g => g.Source.Title == "shared");
        }
    }
}
