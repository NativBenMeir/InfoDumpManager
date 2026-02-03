using System.Diagnostics.CodeAnalysis;
using InfoDumpManager.Application.Agents;
using InfoDumpManager.Application.Agents.Implementations;
using InfoDumpManager.Application.Services.CostManagement;
using InfoDumpManager.Application.Services.Embeddings;
using InfoDumpManager.Application.Services.LLM;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace InfoDumpManager.Tests.Unit.AIAgents;

[ExcludeFromCodeCoverage]
public sealed class CategorizationAgentTests
{
    private readonly Mock<ILLMProvider> _mockLlmProvider;
    private readonly Mock<IEmbeddingProvider> _mockEmbeddingProvider;
    private readonly Mock<IVectorStore> _mockVectorStore;
    private readonly Mock<ICostManager> _mockCostManager;
    private readonly Mock<ILogger<CategorizationAgent>> _mockLogger;
    private readonly CategorizationAgent _agent;

    public CategorizationAgentTests()
    {
        _mockLlmProvider = new Mock<ILLMProvider>();
        _mockEmbeddingProvider = new Mock<IEmbeddingProvider>();
        _mockVectorStore = new Mock<IVectorStore>();
        _mockCostManager = new Mock<ICostManager>();
        _mockLogger = new Mock<ILogger<CategorizationAgent>>();

        _agent = new CategorizationAgent(
            _mockEmbeddingProvider.Object,
            _mockVectorStore.Object,
                        _mockLlmProvider.Object,
            _mockCostManager.Object,
            _mockLogger.Object);
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

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(true, 0.01m, 100m, "BudgetAvailable", "Budget available."));

        _mockEmbeddingProvider
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResponse(new float[] { 0.1f, 0.2f, 0.3f }, "text-embedding-ada-002", "openai", 20, 0.0001m));

        _mockVectorStore
            .Setup(x => x.SearchSimilarAsync(It.IsAny<EmbeddingSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmbeddingSearchResult>
            {
                new EmbeddingSearchResult(categoryId, 0.15, "{\"name\":\"Technology\"}")
            });

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

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(true, 0.01m, 100m, "BudgetAvailable", "Budget available."));

        _mockEmbeddingProvider
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResponse(new float[] { 0.1f, 0.2f, 0.3f }, "text-embedding-ada-002", "openai", 20, 0.0001m));

        _mockVectorStore
            .Setup(x => x.SearchSimilarAsync(It.IsAny<EmbeddingSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmbeddingSearchResult>
            {
                new EmbeddingSearchResult(Guid.NewGuid(), 0.85, "{\"name\":\"Category\"}") // High distance = low similarity
            });

        // Act
        var result = await _agent.ExecuteAsync(context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Confidence);
        Assert.True(result.Confidence.RequiresManualReview);
    }

    [Fact]
    public async Task ExecuteAsync_WithVectorSearchIntegration_ShouldCallVectorStore()
    {
        // Arrange
        var context = CreateTestContext("Content to categorize");

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(true, 0.01m, 100m, "BudgetAvailable", "Budget available."));

        _mockEmbeddingProvider
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResponse(new float[] { 0.1f, 0.2f }, "model", "openai", 10, 0.0001m));

        _mockVectorStore
            .Setup(x => x.SearchSimilarAsync(It.IsAny<EmbeddingSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmbeddingSearchResult>());

        // Act
        await _agent.ExecuteAsync(context);

        // Assert
        _mockVectorStore.Verify(
            x => x.SearchSimilarAsync(It.Is<EmbeddingSearchRequest>(r => r.Limit == 3), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoMatchingCategories_ShouldReturnFallback()
    {
        // Arrange
        var context = CreateTestContext("Uncategorizable content");

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(true, 0.01m, 100m, "BudgetAvailable", "Budget available."));

        _mockEmbeddingProvider
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResponse(new float[] { 0.1f }, "model", "openai", 5, 0.0001m));

        _mockVectorStore
            .Setup(x => x.SearchSimilarAsync(It.IsAny<EmbeddingSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmbeddingSearchResult>());

        // Act
        var result = await _agent.ExecuteAsync(context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Confidence);
        Assert.True(result.Confidence.RequiresManualReview);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCacheEmbeddings()
    {
        // Arrange
        var context = CreateTestContext("Content for embedding");

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(true, 0.01m, 100m, "BudgetAvailable", "Budget available."));

        _mockEmbeddingProvider
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResponse(new float[] { 0.1f }, "model", "openai", 5, 0.0001m));

        _mockVectorStore
            .Setup(x => x.SearchSimilarAsync(It.IsAny<EmbeddingSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmbeddingSearchResult>());

        // Act
        await _agent.ExecuteAsync(context);

        // Assert
        _mockEmbeddingProvider.Verify(
            x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
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
}
