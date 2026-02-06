using System.Diagnostics.CodeAnalysis;
using InfoDumpManager.Application.Agents;
using InfoDumpManager.Application.Agents.Implementations;
using InfoDumpManager.Application.Services.CostManagement;
using InfoDumpManager.Application.Services.LLM;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace InfoDumpManager.Tests.Unit.AIAgents;

[ExcludeFromCodeCoverage]
public sealed class CategorizationAgentTests
{
    private readonly Mock<ILLMProvider> _mockLlmProvider;
    private readonly Mock<ILLMRateLimiter> _mockRateLimiter;
    private readonly Mock<IGEMRepository> _mockGemRepository;
    private readonly Mock<ICategoryRepository> _mockCategoryRepository;
    private readonly Mock<ICostManager> _mockCostManager;
    private readonly Mock<ILogger<CategorizationAgent>> _mockLogger;
    private readonly CategorizationAgent _agent;

    public CategorizationAgentTests()
    {
        _mockLlmProvider = new Mock<ILLMProvider>();
        _mockRateLimiter = new Mock<ILLMRateLimiter>();
        _mockGemRepository = new Mock<IGEMRepository>();
        _mockCategoryRepository = new Mock<ICategoryRepository>();
        _mockCostManager = new Mock<ICostManager>();
        _mockLogger = new Mock<ILogger<CategorizationAgent>>();

        _agent = new CategorizationAgent(
            _mockLlmProvider.Object,
            _mockRateLimiter.Object,
            _mockGemRepository.Object,
            _mockCategoryRepository.Object,
            _mockCostManager.Object,
            _mockLogger.Object);

        _mockRateLimiter
            .Setup(x => x.ExecuteAsync(It.IsAny<Guid>(), It.IsAny<Func<CancellationToken, Task<LLMResponse>>>(), It.IsAny<CancellationToken>()))
            .Returns<Guid, Func<CancellationToken, Task<LLMResponse>>, CancellationToken>((_, func, ct) => func(ct));
    }

    [Fact]
    public void Name_ShouldReturnCorrectValue()
    {
        // Assert
        Assert.Equal("CategorizationAgent", _agent.Name);
    }

    [Fact]
    public void Capability_ShouldReturnCategorization()
    {
        // Assert
        Assert.Equal(AgentCapability.Categorization, _agent.Capability);
    }

    [Fact]
    public async Task ExecuteAsync_WithHighConfidenceMatch_ShouldReturnSuccessWithCategory()
    {
        // Arrange
        var context = CreateTestContext("Article about technology and AI");
        var categoryId = Guid.NewGuid();
        var tenantId = context.TenantId;

        _mockGemRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGem(tenantId));

        _mockCategoryRepository
            .Setup(x => x.ListByTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Category> { CreateCategory(tenantId, categoryId, "Technology") });

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(true, 0.01m, 100m, "BudgetAvailable", "Budget available."));

        _mockLlmProvider
            .Setup(x => x.CallAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse($"{{\"suggested_category_id\":\"{categoryId}\",\"proposed_category_name\":null,\"confidence\":0.85,\"rationale\":\"Matches tech\"}}", "gpt-4", "test", 20, 0.0001m, "completed", 0));

        // Act
        var result = await _agent.ExecuteAsync(context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Confidence);
        Assert.True(result.Confidence.Score > 0.5);
        Assert.Contains("category", result.Data.Payload.Keys);
    }

    [Fact]
    public async Task ExecuteAsync_WithLowConfidence_ShouldFlagForManualReview()
    {
        // Arrange
        var context = CreateTestContext("Ambiguous content");
        var tenantId = context.TenantId;

        _mockGemRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGem(tenantId));

        _mockCategoryRepository
            .Setup(x => x.ListByTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Category> { CreateCategory(tenantId, Guid.NewGuid(), "General") });

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(true, 0.01m, 100m, "BudgetAvailable", "Budget available."));

        _mockLlmProvider
            .Setup(x => x.CallAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse("{\"suggested_category_id\":null,\"proposed_category_name\":\"Misc\",\"confidence\":0.4,\"rationale\":\"Ambiguous\"}", "gpt-4", "test", 20, 0.0001m, "completed", 0));

        // Act
        var result = await _agent.ExecuteAsync(context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Confidence);
        Assert.True(result.Confidence.RequiresManualReview);
    }

    [Fact]
    public async Task ExecuteAsync_WithLLMCall_ShouldUseRateLimiter()
    {
        // Arrange
        var context = CreateTestContext("Content to categorize");
        var tenantId = context.TenantId;

        _mockGemRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGem(tenantId));

        _mockCategoryRepository
            .Setup(x => x.ListByTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Category> { CreateCategory(tenantId, Guid.NewGuid(), "General") });

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(true, 0.01m, 100m, "BudgetAvailable", "Budget available."));

        _mockLlmProvider
            .Setup(x => x.CallAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse("{\"suggested_category_id\":null,\"proposed_category_name\":\"General\",\"confidence\":0.5,\"rationale\":\"Fallback\"}", "gpt-4", "test", 10, 0.0001m, "completed", 0));

        // Act
        await _agent.ExecuteAsync(context);

        // Assert
        _mockRateLimiter.Verify(
            x => x.ExecuteAsync(It.IsAny<Guid>(), It.IsAny<Func<CancellationToken, Task<LLMResponse>>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoMatchingCategories_ShouldReturnFallback()
    {
        // Arrange
        var context = CreateTestContext("Uncategorizable content");
        var tenantId = context.TenantId;

        _mockGemRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGem(tenantId));

        _mockCategoryRepository
            .Setup(x => x.ListByTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Category>());

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(true, 0.01m, 100m, "BudgetAvailable", "Budget available."));

        _mockLlmProvider
            .Setup(x => x.CallAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse(string.Empty, "gpt-4", "test", 5, 0.0001m, "completed", 0));

        // Act
        var result = await _agent.ExecuteAsync(context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Confidence);
        Assert.True(result.Confidence.RequiresManualReview);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFallbackWhenResponseInvalid()
    {
        // Arrange
        var context = CreateTestContext("Content for embedding");
        var tenantId = context.TenantId;

        _mockGemRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGem(tenantId));

        _mockCategoryRepository
            .Setup(x => x.ListByTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Category> { CreateCategory(tenantId, Guid.NewGuid(), "General") });

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(true, 0.01m, 100m, "BudgetAvailable", "Budget available."));

        _mockLlmProvider
            .Setup(x => x.CallAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse("not-json", "gpt-4", "test", 5, 0.0001m, "completed", 0));

        // Act
        var result = await _agent.ExecuteAsync(context);

        // Assert
        Assert.True(result.Success);
    }

    private static AgentContext CreateTestContext(string contentText)
    {
        return new AgentContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            contentText,
            new AgentContextMetadata(
                "test-source",
                100,
                DateTimeOffset.UtcNow,
                new Dictionary<string, object>()));
    }

    private static Category CreateCategory(Guid tenantId, Guid categoryId, string name)
    {
        var category = Category.Create(tenantId, name, Guid.NewGuid(), "desc");
        typeof(Category).GetProperty("Id")!.SetValue(category, categoryId);
        return category;
    }

    private static GEM CreateGem(Guid tenantId)
    {
        var source = new GEMSource("https://example.com", "Example");
        var snapshot = new GEMSnapshot("<html>content</html>");
        return GEM.Create(tenantId, "Title", "https://example.com/page", source, snapshot, GEMSummary.Empty);
    }
}
