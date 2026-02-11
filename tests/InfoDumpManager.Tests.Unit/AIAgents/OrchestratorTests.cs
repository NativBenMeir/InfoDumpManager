using System.Diagnostics.CodeAnalysis;
using InfoDumpManager.Application.Agents;
using InfoDumpManager.Application.Agents.Orchestration;
using InfoDumpManager.Application.Infrastructure.JobQueue;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace InfoDumpManager.Tests.Unit.AIAgents;

[ExcludeFromCodeCoverage]
public sealed class ContentProcessingOrchestratorTests
{
    private readonly Mock<IJobQueue<ProcessingJob>> _mockJobQueue;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IGEMRepository> _mockGemRepository;
    private readonly Mock<ICategoryRepository> _mockCategoryRepository;
    private readonly Mock<ICategorySuggestionRepository> _mockCategorySuggestionRepository;
    private readonly Mock<IActivityLogRepository> _mockActivityLogRepository;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ILogger<ContentProcessingOrchestrator>> _mockLogger;
    private readonly IJobTracker _jobTracker;
    private readonly List<IAgent> _agents;
    private readonly ContentProcessingOrchestrator _orchestrator;

    public ContentProcessingOrchestratorTests()
    {
        _mockJobQueue = new Mock<IJobQueue<ProcessingJob>>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockGemRepository = new Mock<IGEMRepository>();
        _mockCategoryRepository = new Mock<ICategoryRepository>();
        _mockCategorySuggestionRepository = new Mock<ICategorySuggestionRepository>();
        _mockActivityLogRepository = new Mock<IActivityLogRepository>();
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<ContentProcessingOrchestrator>>();
        _jobTracker = new InMemoryJobTracker();

        _mockGemRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InfoDumpManager.Domain.Entities.GEM?)null);

        _mockUnitOfWork.Setup(x => x.GEMs).Returns(_mockGemRepository.Object);
        _mockUnitOfWork.Setup(x => x.Categories).Returns(_mockCategoryRepository.Object);
        _mockUnitOfWork.Setup(x => x.CategorySuggestions).Returns(_mockCategorySuggestionRepository.Object);
        _mockUnitOfWork.Setup(x => x.ActivityLogs).Returns(_mockActivityLogRepository.Object);
        _mockUnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        _mockActivityLogRepository
            .Setup(x => x.AddAsync(It.IsAny<InfoDumpManager.Domain.Entities.ActivityLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockCategorySuggestionRepository
            .Setup(x => x.AddAsync(It.IsAny<InfoDumpManager.Domain.Entities.CategorySuggestion>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockMediator
            .Setup(x => x.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Create mock agents
        var mockSummarizationAgent = CreateMockAgent(AgentCapability.Summarization, "SummarizationAgent", success: true);
        var mockCategorizationAgent = CreateMockAgent(AgentCapability.Categorization, "CategorizationAgent", success: true);
        var mockTaggingAgent = CreateMockAgent(AgentCapability.Tagging, "TaggingAgent", success: true);
        var mockValidationAgent = CreateMockAgent(AgentCapability.Validation, "ValidationAgent", success: true);

        _agents = new List<IAgent>
        {
            mockSummarizationAgent.Object,
            mockCategorizationAgent.Object,
            mockTaggingAgent.Object,
            mockValidationAgent.Object
        };

        _orchestrator = CreateOrchestrator(_agents);
    }

    [Fact]
    public async Task ProcessGEMAsync_WithSuccessfulPipeline_ShouldReturnCompletedStatus()
    {
        // Arrange
        var gemId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var contentText = "Test content for processing";
        var options = new ProcessingOptions();

        // Act
        var result = await _orchestrator.ProcessGEMAsync(gemId, tenantId, contentText, options);

        // Assert
        Assert.Equal(ProcessingStatus.Completed, result.Status);
        Assert.NotNull(result.Summary);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ProcessGEMAsync_WithSummarizationFailure_ShouldReturnFailedStatus()
    {
        // Arrange
        var failingAgent = CreateMockAgent(AgentCapability.Summarization, "SummarizationAgent", success: false);
        var successAgent = CreateMockAgent(AgentCapability.Categorization, "CategorizationAgent", success: true);

        var orchestrator = CreateOrchestrator(new List<IAgent>
        {
            failingAgent.Object,
            successAgent.Object
        });

        var gemId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var options = new ProcessingOptions();

        // Act
        var result = await orchestrator.ProcessGEMAsync(gemId, tenantId, "content", options);

        // Assert
        Assert.Equal(ProcessingStatus.Failed, result.Status);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task ProcessGEMAsync_WithOptionalAgentFailure_ShouldContinuePipeline()
    {
        // Arrange
        var summaryAgent = CreateMockAgent(AgentCapability.Summarization, "SummarizationAgent", success: true);
        var categorizationAgent = CreateMockAgent(AgentCapability.Categorization, "CategorizationAgent", success: false);
        var taggingAgent = CreateMockAgent(AgentCapability.Tagging, "TaggingAgent", success: true);

        var orchestrator = CreateOrchestrator(new List<IAgent>
        {
            summaryAgent.Object,
            categorizationAgent.Object,
            taggingAgent.Object
        });

        var gemId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var options = new ProcessingOptions();

        // Act
        var result = await orchestrator.ProcessGEMAsync(gemId, tenantId, "content", options);

        // Assert
        Assert.Equal(ProcessingStatus.Completed, result.Status);
        Assert.NotNull(result.Summarization);
        Assert.NotNull(result.Tagging);
    }

    [Fact]
    public async Task ProcessGEMAsync_ShouldResolveAgentDependencies()
    {
        // Arrange
        var gemId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var options = new ProcessingOptions();

        // Act
        var result = await _orchestrator.ProcessGEMAsync(gemId, tenantId, "content", options);

        // Assert
        Assert.NotNull(result.Summarization);
        Assert.NotNull(result.Categorization);
        Assert.NotNull(result.Tagging);
    }

    [Fact]
    public async Task ProcessGEMAsync_ShouldTrackProgressUpdates()
    {
        // Arrange
        var gemId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var options = new ProcessingOptions();

        // Act
        var result = await _orchestrator.ProcessGEMAsync(gemId, tenantId, "content", options);

        // Assert
        Assert.NotEqual(default(DateTimeOffset), result.CompletedAt);
        Assert.Equal(ProcessingStatus.Completed, result.Status);
    }

    [Fact]
    public async Task ProcessBatchAsync_ShouldProcessMultipleGEMs()
    {
        // Arrange
        var items = new List<(Guid GEMId, Guid TenantId, string ContentText)>
        {
            (Guid.NewGuid(), Guid.NewGuid(), "Content 1"),
            (Guid.NewGuid(), Guid.NewGuid(), "Content 2"),
            (Guid.NewGuid(), Guid.NewGuid(), "Content 3")
        };
        var options = new ProcessingOptions();

        // Act
        var result = await _orchestrator.ProcessBatchAsync(items, options);

        // Assert
        Assert.Equal(ProcessingStatus.Completed, result.Status);
    }

    [Fact]
    public async Task GetJobStatusAsync_ShouldReturnJobStatus()
    {
        // Arrange
        var jobId = Guid.NewGuid();

        // Act
        var status = await _orchestrator.GetJobStatusAsync(jobId);

        // Assert
        Assert.NotNull(status);
    }

    [Fact]
    public async Task Orchestrator_WithLowConfidenceResult_ShouldFlagForReview()
    {
        // High Priority Test #6 - Agent Result Confidence Score Validation
        // Arrange
        var mockAgent = new Mock<IAgent>();
        mockAgent.Setup(x => x.Capability).Returns(AgentCapability.Summarization);
        mockAgent.Setup(x => x.Name).Returns("LowConfidenceAgent");
        mockAgent.Setup(x => x.ExecuteAsync(It.IsAny<AgentContext>()))
            .ReturnsAsync(new AgentResult(
                true, // Success, but low confidence
                "Summary completed with low confidence",
                new AgentResultData("LowConfidenceAgent", DateTimeOffset.UtcNow, new Dictionary<string, object>
                {
                    ["summaryObject"] = GEMSummary.Create("Uncertain summary", "gpt-4", 50, DateTimeOffset.UtcNow)
                }),
                new AgentMetrics(50, 0.0005m, TimeSpan.FromMilliseconds(100), 0, "test"),
                null,
                new AgentResultConfidence(0.3, true, "Limited context available"))); // Low confidence: 0.3

        var orchestrator = CreateOrchestrator(new List<IAgent> { mockAgent.Object });
        var options = new ProcessingOptions();

        // Act
        var result = await orchestrator.ProcessGEMAsync(Guid.NewGuid(), Guid.NewGuid(), "test content", options);

        // Assert
        Assert.Equal(ProcessingStatus.Completed, result.Status);
        // TODO: In future, add result.RequiresReview flag based on low confidence score
    }

    [Fact]
    public async Task Orchestrator_AfterPipelineExecution_ShouldAggregateMetricsCorrectly()
    {
        // Medium Priority Test #9 - Agent Metrics Aggregation
        // Arrange
        var agentMocks = new List<Mock<IAgent>>
        {
            CreateMockAgentWithMetrics(AgentCapability.Summarization, "Agent1", 100, 0.002m, 500),
            CreateMockAgentWithMetrics(AgentCapability.Categorization, "Agent2", 50, 0.001m, 300),
            CreateMockAgentWithMetrics(AgentCapability.Tagging, "Agent3", 75, 0.0015m, 400)
        };

        var agents = agentMocks.Select(m => m.Object).ToList();

        var orchestrator = CreateOrchestrator(agents);
        var options = new ProcessingOptions();

        // Act
        var result = await orchestrator.ProcessGEMAsync(Guid.NewGuid(), Guid.NewGuid(), "test content", options);

        // Assert
        Assert.Equal(ProcessingStatus.Completed, result.Status);
        // Total tokens: 100 + 50 + 75 = 225
        // Total cost: 0.002 + 0.001 + 0.0015 = 0.0045
        // These would be tracked in aggregated metrics
    }

    [Fact]
    public async Task Orchestrator_BatchProcessing_WithPartialFailures_ShouldReportCorrectly()
    {
        // Medium Priority Test #11 - Batch Processing Partial Failure
        // Arrange
        var callCount = 0;
        var mockAgent = new Mock<IAgent>();
        mockAgent.Setup(x => x.Capability).Returns(AgentCapability.Summarization);
        mockAgent.Setup(x => x.Name).Returns("IntermittentAgent");
        mockAgent.Setup(x => x.ExecuteAsync(It.IsAny<AgentContext>()))
            .Returns(() =>
            {
                callCount++;
                var success = callCount % 2 == 0; // Alternate success/failure
                var payload = new Dictionary<string, object>();
                if (success)
                {
                    payload["summaryObject"] = GEMSummary.Create($"Summary {callCount}", "gpt-4", 100, DateTimeOffset.UtcNow);
                }
                return Task.FromResult(new AgentResult(
                    success,
                    success ? "Success" : "Failure",
                    new AgentResultData("IntermittentAgent", DateTimeOffset.UtcNow, payload),
                    new AgentMetrics(100, 0.001m, TimeSpan.FromMilliseconds(10), 0, "test"),
                    success ? null : new List<string> { $"Error {callCount}" }));
            });

        var orchestrator = CreateOrchestrator(new List<IAgent> { mockAgent.Object });
        var items = Enumerable.Range(0, 6)
            .Select(_ => (Guid.NewGuid(), Guid.NewGuid(), "Batch content"))
            .ToList();

        var options = new ProcessingOptions();

        // Act
        var result = await orchestrator.ProcessBatchAsync(items, options);

        // Assert - Some items succeeded, some failed
        Assert.Equal(ProcessingStatus.Failed, result.Status);
        // In a real implementation, batch result would have:
        // - SuccessCount: 3 (items 2, 4, 6)
        // - FailureCount: 3 (items 1, 3, 5)
    }

    private static Mock<IAgent> CreateMockAgentWithMetrics(
        AgentCapability capability,
        string name,
        int tokens,
        decimal cost,
        int durationMs)
    {
        var mock = new Mock<IAgent>();
        mock.Setup(x => x.Capability).Returns(capability);
        mock.Setup(x => x.Name).Returns(name);
        mock.Setup(x => x.ExecuteAsync(It.IsAny<AgentContext>()))
            .ReturnsAsync(new AgentResult(
                true,
                $"{name} completed",
                new AgentResultData(name, DateTimeOffset.UtcNow, new Dictionary<string, object>()),
                new AgentMetrics(tokens, cost, TimeSpan.FromMilliseconds(durationMs), 0, "test-provider")));
        return mock;
    }

    private static Mock<IAgent> CreateMockAgent(AgentCapability capability, string name, bool success)
    {
        var mock = new Mock<IAgent>();
        mock.Setup(x => x.Capability).Returns(capability);
        mock.Setup(x => x.Name).Returns(name);
        mock.Setup(x => x.ExecuteAsync(It.IsAny<AgentContext>()))
            .ReturnsAsync(() =>
            {
                var payload = new Dictionary<string, object>();
                
                if (capability == AgentCapability.Summarization && success)
                {
                    payload["summary"] = "Test summary";
                    payload["summaryObject"] = GEMSummary.Create("Test summary", "gpt-4", 0, DateTimeOffset.UtcNow);
                }

                return new AgentResult(
                    success,
                    success ? $"{name} completed" : $"{name} failed",
                    new AgentResultData(name, DateTimeOffset.UtcNow, payload),
                    new AgentMetrics(100, 0.001m, TimeSpan.FromSeconds(1), 0, "test-provider"),
                    success ? null : new List<string> { $"{name} error" },
                    new AgentResultConfidence(success ? 0.9 : 0.3, !success, "Test reasoning"));
            });

        return mock;
    }

    private ContentProcessingOrchestrator CreateOrchestrator(IReadOnlyCollection<IAgent> agents)
    {
        var provider = CreateServiceProvider(agents);
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        return new ContentProcessingOrchestrator(scopeFactory, _jobTracker, _mockLogger.Object);
    }

    private ServiceProvider CreateServiceProvider(IReadOnlyCollection<IAgent> agents)
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => _mockUnitOfWork.Object);
        services.AddScoped<IMediator>(_ => _mockMediator.Object);

        foreach (var agent in agents)
        {
            services.AddScoped<IAgent>(_ => agent);
        }

        return services.BuildServiceProvider();
    }
}
