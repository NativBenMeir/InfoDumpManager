using System.Diagnostics.CodeAnalysis;
using InfoDumpManager.Application.Agents;
using InfoDumpManager.Application.Agents.Orchestration;
using InfoDumpManager.Application.Infrastructure.JobQueue;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Domain.ValueObjects;
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
    private readonly Mock<ILogger<ContentProcessingOrchestrator>> _mockLogger;
    private readonly List<IAgent> _agents;
    private readonly ContentProcessingOrchestrator _orchestrator;

    public ContentProcessingOrchestratorTests()
    {
        _mockJobQueue = new Mock<IJobQueue<ProcessingJob>>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockGemRepository = new Mock<IGEMRepository>();
        _mockLogger = new Mock<ILogger<ContentProcessingOrchestrator>>();

        _mockGemRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InfoDumpManager.Domain.Entities.GEM?)null);

        _mockUnitOfWork.Setup(x => x.GEMs).Returns(_mockGemRepository.Object);
        _mockUnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);

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
        Assert.NotNull(result.CompletedAt);
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
        return new ContentProcessingOrchestrator(scopeFactory, _mockLogger.Object);
    }

    private ServiceProvider CreateServiceProvider(IReadOnlyCollection<IAgent> agents)
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => _mockUnitOfWork.Object);

        foreach (var agent in agents)
        {
            services.AddScoped<IAgent>(_ => agent);
        }

        return services.BuildServiceProvider();
    }
}
