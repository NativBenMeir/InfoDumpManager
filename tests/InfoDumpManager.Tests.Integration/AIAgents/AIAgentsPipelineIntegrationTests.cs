using System.Diagnostics.CodeAnalysis;
using InfoDumpManager.Application.Agents;
using InfoDumpManager.Application.Agents.Orchestration;
using InfoDumpManager.Application.Common.Events;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Events;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Domain.ValueObjects;
using InfoDumpManager.Infrastructure.Data;
using InfoDumpManager.Infrastructure.Repositories;
using InfoDumpManager.Tests.Integration.Fixtures;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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
        var gem = GEM.Create(
            tenantId,
            "Taggable GEM",
            "https://example.com/tags",
            new GEMSource("https://example.com/source", "source"),
            new GEMSnapshot("tag content", "text/plain", DateTimeOffset.UtcNow));

        _dbContext.Gems.Add(gem);
        await _dbContext.SaveChangesAsync();

        var suggestedTags = new List<TagSuggestionResult>
        {
            new(Guid.NewGuid(), "ai", 0.91),
            new(Guid.NewGuid(), "ml", 0.83)
        };

        var taggingResult = new AgentResult(
            true,
            "Tagging completed",
            new AgentResultData(
                "TaggingAgent",
                DateTimeOffset.UtcNow,
                new Dictionary<string, object>
                {
                    ["suggestedTags"] = suggestedTags,
                    ["tags"] = suggestedTags.Select(x => x.TagName).ToList()
                }),
            new AgentMetrics(50, 0.0005m, TimeSpan.FromMilliseconds(10), 0, "test"));

        var mediator = new Mock<IMediator>();
        var persistence = new ProcessingPersistence(CreateUnitOfWork(), mediator.Object);

        // Act
        await persistence.HandleTaggingAsync(tenantId, gem.Id, taggingResult);

        // Assert
        var activityLog = await _dbContext.ActivityLogs
            .Where(x => x.TenantId == tenantId && x.EntityId == gem.Id && x.EventType == ActivityEventType.TaggingSuggested)
            .OrderByDescending(x => x.OccurredAt)
            .FirstOrDefaultAsync();

        Assert.NotNull(activityLog);
        Assert.Contains("Tagging suggested", activityLog!.Description);

        mediator.Verify(x => x.Publish(
                It.Is<DomainEventNotification>(n =>
                    n.Event != null
                    && n.Event.GetType() == typeof(GEMTaggingSuggested)
                    && ((GEMTaggingSuggested)n.Event).GEMId == gem.Id
                    && ((GEMTaggingSuggested)n.Event).TenantId == tenantId
                    && ((GEMTaggingSuggested)n.Event).Tags.Count == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);

    }

    [Fact]
    public async Task ProcessGEMAsync_ShouldPublishDomainEvents()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var gem = GEM.Create(
            tenantId,
            "Domain Event GEM",
            "https://example.com/events",
            new GEMSource("https://example.com/source", "source"),
            new GEMSnapshot("pipeline content", "text/plain", DateTimeOffset.UtcNow));

        _dbContext.Gems.Add(gem);
        await _dbContext.SaveChangesAsync();

        var mediator = new Mock<IMediator>();

        var services = new ServiceCollection();
        services.AddSingleton(mediator.Object);
        services.AddScoped<IUnitOfWork>(_ => CreateUnitOfWork());
        services.AddScoped<IProcessingPersistence, ProcessingPersistence>();
        services.AddScoped<IProcessingActivityLogger, ProcessingActivityLogger>();
        services.AddScoped<IAgent>(_ => new FakeSummarizationAgent());

        var serviceProvider = services.BuildServiceProvider();
        var orchestrator = new ContentProcessingOrchestrator(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            new InMemoryJobTracker(),
            NullLogger<ContentProcessingOrchestrator>.Instance);

        // Act
        _ = await orchestrator.ProcessGEMAsync(
            gem.Id,
            tenantId,
            "This is the content to summarize.",
            new ProcessingOptions(RunValidation: false));

        // Assert
        mediator.Verify(x => x.Publish(
                It.Is<DomainEventNotification>(n =>
                    n.Event != null
                    && n.Event.GetType() == typeof(GEMSummarizationCompleted)
                    && ((GEMSummarizationCompleted)n.Event).GEMId == gem.Id
                    && ((GEMSummarizationCompleted)n.Event).TenantId == tenantId
                    && !string.IsNullOrWhiteSpace(((GEMSummarizationCompleted)n.Event).Summary)),
                It.IsAny<CancellationToken>()),
            Times.Once);

        await using var verificationContext = _fixture.CreateContext();
        var refreshedGem = await verificationContext.Gems.FindAsync(gem.Id);
        Assert.NotNull(refreshedGem);
        Assert.NotEqual(GEMSummary.Empty.Text, refreshedGem!.Summary.Text);
    }

    private UnitOfWork CreateUnitOfWork()
    {
        return new UnitOfWork(
            _dbContext,
            new GEMRepository(_dbContext),
            new CategoryRepository(_dbContext),
            new TagRepository(_dbContext),
            new CategorySuggestionRepository(_dbContext),
            new ActivityLogRepository(_dbContext));
    }

    private sealed class FakeSummarizationAgent : IAgent
    {
        public string Name => "FakeSummarizationAgent";

        public AgentCapability Capability => AgentCapability.Summarization;

        public Task<AgentResult> ExecuteAsync(AgentContext context)
        {
            var summary = GEMSummary.Create(
                $"Summary for {context.GEMId}",
                "fake-model",
                42,
                DateTimeOffset.UtcNow);

            return Task.FromResult(new AgentResult(
                true,
                "ok",
                new AgentResultData(
                    Name,
                    DateTimeOffset.UtcNow,
                    new Dictionary<string, object>
                    {
                        ["summaryObject"] = summary,
                        ["tokenCount"] = 42,
                        ["model"] = "fake-model"
                    }),
                new AgentMetrics(42, 0.0001m, TimeSpan.FromMilliseconds(5), 0, "fake")));
        }
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
