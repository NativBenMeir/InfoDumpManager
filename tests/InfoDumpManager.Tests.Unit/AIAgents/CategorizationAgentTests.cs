using System.Diagnostics.CodeAnalysis;
using InfoDumpManager.Application.Agents;
using InfoDumpManager.Application.Agents.Implementations;
using InfoDumpManager.Application.Services.CostManagement;
using InfoDumpManager.Application.Services.LLM;
using InfoDumpManager.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace InfoDumpManager.Tests.Unit.AIAgents;

[ExcludeFromCodeCoverage]
public sealed class CategorizationAgentTests
{
    private readonly Mock<ILLMProvider> _mockLlmProvider;
    private readonly Mock<ILLMRateLimiter> _mockRateLimiter;
    private readonly Mock<ICostManager> _mockCostManager;
    private readonly Mock<ILogger<CategorizationAgent>> _mockLogger;
    private readonly CategorizationAgent _agent;

    public CategorizationAgentTests()
    {
        _mockLlmProvider = new Mock<ILLMProvider>();
        _mockRateLimiter = new Mock<ILLMRateLimiter>();
        _mockCostManager = new Mock<ICostManager>();
        _mockLogger = new Mock<ILogger<CategorizationAgent>>();

        _agent = new CategorizationAgent(
            _mockLlmProvider.Object,
            _mockRateLimiter.Object,
            _mockCostManager.Object,
            Options.Create(CreateLlmSettings()),
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
        var categoryId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var categories = new List<Category> { CreateCategory(tenantId, categoryId, "Technology") };
        var context = CreateTestContextWithCategories(tenantId, "Article about technology and AI", categories);

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(true, 0.01m, 100m, "BudgetAvailable", "Budget available."));

        _mockLlmProvider
            .Setup(x => x.CallAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyDictionary<string, string>?>()))
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
        var tenantId = Guid.NewGuid();
        var categories = new List<Category> { CreateCategory(tenantId, Guid.NewGuid(), "General") };
        var context = CreateTestContextWithCategories(tenantId, "Ambiguous content", categories);

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(true, 0.01m, 100m, "BudgetAvailable", "Budget available."));

        _mockLlmProvider
            .Setup(x => x.CallAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyDictionary<string, string>?>()))
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
        var tenantId = Guid.NewGuid();
        var categories = new List<Category> { CreateCategory(tenantId, Guid.NewGuid(), "General") };
        var context = CreateTestContextWithCategories(tenantId, "Content to categorize", categories);

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(true, 0.01m, 100m, "BudgetAvailable", "Budget available."));

        _mockLlmProvider
            .Setup(x => x.CallAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyDictionary<string, string>?>()))
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
        var tenantId = Guid.NewGuid();
        var context = CreateTestContextWithCategories(tenantId, "Uncategorizable content", new List<Category>());

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(true, 0.01m, 100m, "BudgetAvailable", "Budget available."));

        _mockLlmProvider
            .Setup(x => x.CallAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyDictionary<string, string>?>()))
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
        var tenantId = Guid.NewGuid();
        var categories = new List<Category> { CreateCategory(tenantId, Guid.NewGuid(), "General") };
        var context = CreateTestContextWithCategories(tenantId, "Content for embedding", categories);

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(true, 0.01m, 100m, "BudgetAvailable", "Budget available."));

        _mockLlmProvider
            .Setup(x => x.CallAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyDictionary<string, string>?>()))
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

    private static AgentContext CreateTestContextWithCategories(Guid tenantId, string contentText, IReadOnlyCollection<Category> categories)
    {
        var customData = new Dictionary<string, object> { ["categories"] = categories };
        return new AgentContext(
            Guid.NewGuid(),
            tenantId,
            contentText,
            new AgentContextMetadata(
                "test-source",
                100,
                DateTimeOffset.UtcNow,
                customData));
    }

    private static Category CreateCategory(Guid tenantId, Guid categoryId, string name)
    {
        var category = Category.Create(tenantId, name, Guid.NewGuid(), "desc");
        typeof(Category).GetProperty("Id")!.SetValue(category, categoryId);
        return category;
    }

    private static AgentLlmSettings CreateLlmSettings()
    {
        return new AgentLlmSettings
        {
            Agents = new Dictionary<string, AgentLlmAgentSettings>(StringComparer.OrdinalIgnoreCase)
            {
                ["CategorizationAgent"] = new AgentLlmAgentSettings
                {
                    Chat = new LlmEndpointSettings { Provider = "OpenAI", Model = "gpt-4" },
                    Embedding = new LlmEndpointSettings { Provider = "OpenAI", Model = "text-embedding-3-large" }
                }
            }
        };
    }
}
