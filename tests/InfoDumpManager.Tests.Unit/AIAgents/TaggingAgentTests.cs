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
public sealed class TaggingAgentTests
{
    private readonly Mock<ILLMProvider> _mockLlmProvider;
    private readonly Mock<IEmbeddingProvider> _mockEmbeddingProvider;
    private readonly Mock<IVectorStore> _mockVectorStore;
    private readonly Mock<ICostManager> _mockCostManager;
    private readonly Mock<ILogger<TaggingAgent>> _mockLogger;
    private readonly TaggingAgent _agent;

    public TaggingAgentTests()
    {
        _mockLlmProvider = new Mock<ILLMProvider>();
        _mockEmbeddingProvider = new Mock<IEmbeddingProvider>();
        _mockVectorStore = new Mock<IVectorStore>();
        _mockCostManager = new Mock<ICostManager>();
        _mockLogger = new Mock<ILogger<TaggingAgent>>();

        _agent = new TaggingAgent(
            _mockLlmProvider.Object,
            _mockEmbeddingProvider.Object,
            _mockVectorStore.Object,
            _mockCostManager.Object,
            _mockLogger.Object);
    }

    [Fact]
    public void Name_ShouldReturnCorrectValue()
    {
        // Assert
        Assert.Equal("TaggingAgent", _agent.Name);
    }

    [Fact]
    public void Capability_ShouldReturnTagging()
    {
        // Assert
        Assert.Equal(AgentCapability.Tagging, _agent.Capability);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldExtractTagsFromLLMResponse()
    {
        // Arrange
        var context = CreateTestContext("Article about machine learning and neural networks");

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(true, 0.01m, 100m, "BudgetAvailable", "Budget available."));

        _mockLlmProvider
            .Setup(x => x.CallAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse("machine-learning, neural-networks, ai, technology", "gpt-4", "test", 30, 0.001m, "completed", 0));

        _mockEmbeddingProvider
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResponse(new float[] { 0.1f, 0.2f }, "model", "openai", 10, 0.0001m));

        // Act
        var result = await _agent.ExecuteAsync(context);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.Data.Payload.ContainsKey("tags"));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldDeduplicateTags()
    {
        // Arrange
        var context = CreateTestContext("Content with duplicate concepts");

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(true, 0.01m, 100m, "BudgetAvailable", "Budget available."));

        _mockLlmProvider
            .Setup(x => x.CallAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse("technology, tech, technology, innovation", "gpt-4", "test", 20, 0.001m, "completed", 0));

        _mockEmbeddingProvider
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResponse(new float[] { 0.1f }, "model", "openai", 5, 0.0001m));

        // Act
        var result = await _agent.ExecuteAsync(context);

        // Assert
        Assert.True(result.Success);
        var tags = result.Data.Payload["tags"] as List<string>;
        Assert.NotNull(tags);
        Assert.Equal(tags.Count, tags.Distinct().Count());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStoreEmbeddingsForTags()
    {
        // Arrange
        var context = CreateTestContext("Content to tag");

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(true, 0.01m, 100m, "BudgetAvailable", "Budget available."));

        _mockLlmProvider
            .Setup(x => x.CallAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse("tag1, tag2", "gpt-4", "test", 20, 0.001m, "completed", 0));

        _mockEmbeddingProvider
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResponse(new float[] { 0.1f, 0.2f }, "model", "openai", 10, 0.0001m));

        // Act
        await _agent.ExecuteAsync(context);

        // Assert
        _mockVectorStore.Verify(
            x => x.StoreAsync(It.IsAny<EmbeddingRecord>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmbeddingFailure_ShouldHandleGracefully()
    {
        // Arrange
        var context = CreateTestContext("Content to tag");

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(true, 0.01m, 100m, "BudgetAvailable", "Budget available."));

        _mockLlmProvider
            .Setup(x => x.CallAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse("tag1", "gpt-4", "test", 10, 0.001m, "completed", 0));

        _mockEmbeddingProvider
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Embedding service down"));

        // Act
        var result = await _agent.ExecuteAsync(context);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Errors);
        Assert.Contains(result.Errors, e => e.Contains("Embedding service down"));
    }

    [Fact]
    public async Task ExecuteAsync_WithCostBudgetDenied_ShouldNotCallLLM()
    {
        // Arrange
        var context = CreateTestContext("Content to tag");

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(false, 0m, 0m, "BudgetExceeded", "Budget exceeded"));

        // Act
        var result = await _agent.ExecuteAsync(context);

        // Assert
        Assert.False(result.Success);
        _mockLlmProvider.Verify(
            x => x.CallAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
