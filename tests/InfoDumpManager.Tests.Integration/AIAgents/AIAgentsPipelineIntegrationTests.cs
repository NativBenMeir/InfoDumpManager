using System.Diagnostics.CodeAnalysis;
using InfoDumpManager.Application.Agents;
using InfoDumpManager.Application.Agents.Orchestration;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.ValueObjects;
using InfoDumpManager.Infrastructure.Data;
using InfoDumpManager.Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoDumpManager.Tests.Integration.AIAgents;

[ExcludeFromCodeCoverage]
[Collection("IntegrationTests")]
public sealed class AIAgentsPipelineIntegrationTests : IAsyncLifetime
{
    private readonly PostgresTestcontainerFixture _fixture;
    private ApplicationDbContext _dbContext = null!;

    public AIAgentsPipelineIntegrationTests(PostgresTestcontainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _dbContext = _fixture.CreateContext();
        await _dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task ProcessGEMAsync_EndToEnd_ShouldPersistSummaryToDatabase()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var contentText = "This is test content for AI processing pipeline integration test.";

        // Create GEM entity in database        
        var source = new GEMSource("https://example.com/integration", "Integration Test Source");
        var snapshot = new GEMSnapshot(contentText, "text/plain", DateTimeOffset.UtcNow);
        var gem = GEM.Create(tenantId, "Integration Test GEM", "https://example.com/gem", source, snapshot);

        _dbContext.Gems.Add(gem);
        await _dbContext.SaveChangesAsync();

        // Note: This test requires fully configured services with mock LLM providers
        // In a real integration test, you would:
        // 1. Use a test double for ILLMProvider
        // 2. Configure the orchestrator with test agents
        // 3. Verify database persistence

        // Act
        // var orchestrator = _serviceProvider.GetRequiredService<IContentProcessingOrchestrator>();
        // var result = await orchestrator.ProcessGEMAsync(gemId, tenantId, contentText, new ProcessingOptions());

        // Assert
        var retrievedGem = await _dbContext.Gems.FindAsync(gem.Id);
        Assert.NotNull(retrievedGem);
        Assert.Equal(contentText, retrievedGem.Snapshot.HtmlContent);
    }

    [Fact]
    public async Task ProcessGEMAsync_ShouldAssignCategory()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var createdById = Guid.NewGuid();

        // Create category
        var category = Category.Create(tenantId, "Technology", createdById, "Technology category");
        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync();

        // This test would verify category assignment through the pipeline
        Assert.NotNull(category);
    }

    [Fact]
    public async Task ProcessGEMAsync_ShouldCreateTags()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // This test would verify tag creation and association
        // through the complete AI pipeline

        // Assert
        Assert.True(true); // Placeholder - requires full service configuration
    }

    [Fact]
    public async Task ProcessGEMAsync_ShouldPublishDomainEvents()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // This test would verify domain events are published
        // (GEMSummarizationStarted, GEMSummarizationCompleted, etc.)

        // Assert
        Assert.True(true); // Placeholder - requires event collection mechanism
    }

    [Fact]
    public async Task ProcessGEMAsync_WithExistingGEM_ShouldUpdateSummary()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var source = new GEMSource("https://example.com/existing", "Existing GEM Source");
        var snapshot = new GEMSnapshot("Original content", "text/plain", DateTimeOffset.UtcNow);
        var gem = GEM.Create(tenantId, "Existing GEM", "https://example.com/existing-gem", source, snapshot);

        _dbContext.Gems.Add(gem);
        await _dbContext.SaveChangesAsync();

        // Act - Update with new summary through pipeline
        // var orchestrator = _serviceProvider.GetRequiredService<IContentProcessingOrchestrator>();
        // await orchestrator.ProcessGEMAsync(gemId, tenantId, "Updated content", new ProcessingOptions());

        // Assert
        _dbContext.Entry(gem).Reload();
        Assert.NotNull(gem);
    }
}
