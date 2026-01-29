using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;

using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.ValueObjects;
using InfoDumpManager.Infrastructure.Data;
using InfoDumpManager.Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoDumpManager.Tests.Integration;

[Collection("IntegrationTests")]
public sealed class EfCoreIntegrationTests
{
    private readonly PostgresTestcontainerFixture _fixture;

    public EfCoreIntegrationTests(PostgresTestcontainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DbContextCanConnect()
    {
        await using var context = _fixture.CreateContext();
        Assert.True(await context.Database.CanConnectAsync());
    }

    [Fact]
    public async Task MigrationsApplySuccessfully()
    {
        await using var context = _fixture.CreateContext();
        await context.Database.MigrateAsync();

        var pending = await context.Database.GetPendingMigrationsAsync();
        Assert.Empty(pending);
    }

    [Fact]
    public async Task GemMappingAllowsInsertAndRetrieve()
    {
        var tenantId = Guid.NewGuid();
        var category = Category.Create(tenantId, "Integration Category", Guid.NewGuid());
        var gem = CreateSampleGem(tenantId, category);

        await using var context = _fixture.CreateContext();
        await context.AddAsync(category);
        await context.SaveChangesAsync();

        var loaded = await context.Gems
            .Include(g => g.Category)
            .SingleAsync(g => g.Id == gem.Id);

        Assert.Equal(gem.Title, loaded.Title);
        Assert.Equal(category.Name, loaded.Category?.Name);
        Assert.Equal(gem.Summary.Text, loaded.Summary.Text);
    }

    [Fact]
    public async Task CategoryMappingAllowsInsertAndRetrieve()
    {
        var tenantId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var category = Category.Create(tenantId, "Category Mapping", createdBy, "Integration tests");

        await using var context = _fixture.CreateContext();
        await context.AddAsync(category);
        await context.SaveChangesAsync();

        var loaded = await context.Categories.FindAsync(category.Id);

        Assert.NotNull(loaded);
        Assert.Equal(category.Name, loaded!.Name);
        Assert.Equal(createdBy, loaded.CreatedById);
        Assert.Equal("Integration tests", loaded.Description);
    }

    [Fact]
    public async Task ForeignKeyRestrictionPreventsCategoryDeletion()
    {
        var tenantId = Guid.NewGuid();
        var category = Category.Create(tenantId, "Protected Category", Guid.NewGuid());
        _ = CreateSampleGem(tenantId, category);

        await using var setupContext = _fixture.CreateContext();
        await setupContext.AddAsync(category);
        await setupContext.SaveChangesAsync();

        await using var deleteContext = _fixture.CreateContext();
        var categoryToDelete = await deleteContext.Categories.FindAsync(category.Id);
        Assert.NotNull(categoryToDelete);

        deleteContext.Categories.Remove(categoryToDelete!);
        await Assert.ThrowsAsync<DbUpdateException>(() => deleteContext.SaveChangesAsync());
    }

    [Fact]
    public async Task IndexesExistOnCommonlyQueriedColumns()
    {
        await using var context = _fixture.CreateContext();
        await context.Database.OpenConnectionAsync();

        try
        {
            using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT indexname FROM pg_indexes WHERE schemaname = 'public' AND indexname IN ('IX_Gems_TenantId_Title','IX_Categories_TenantId_Name','IX_ActivityLogs_TenantId_EventType')";

            var existingIndexes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                existingIndexes.Add(reader.GetString(0));
            }

            var expectedIndexes = new[]
            {
                "IX_Gems_TenantId_Title",
                "IX_Categories_TenantId_Name",
                "IX_ActivityLogs_TenantId_EventType"
            };

            foreach (var index in expectedIndexes)
            {
                Assert.Contains(index, existingIndexes);
            }
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static GEM CreateSampleGem(Guid tenantId, Category category)
    {
        var source = new GEMSource("https://example.com", "Example Source");
        var snapshot = new GEMSnapshot("<html><body>Snapshot</body></html>");
        var summary = GEMSummary.Create("Integration summary", "gpt-4", 42, DateTimeOffset.UtcNow);
        var gem = GEM.Create(tenantId, "Integration GEM", "https://example.com", source, snapshot, summary);
        category.AddGem(gem);
        return gem;
    }
}
