using System.Diagnostics.CodeAnalysis;
using InfoDumpManager.Application.Agents;
using InfoDumpManager.Application.Agents.Implementations;
using InfoDumpManager.Application.Services.Caching;
using InfoDumpManager.Application.Services.CostManagement;
using InfoDumpManager.Application.Services.Embeddings;
using InfoDumpManager.Application.Services.LLM;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace InfoDumpManager.Tests.Unit.AIAgents;

[ExcludeFromCodeCoverage]
public sealed class TaggingAgentTests
{
    private readonly Mock<IEmbeddingProvider> _mockEmbeddingProvider;
    private readonly Mock<IVectorStore> _mockVectorStore;
    private readonly Mock<IEmbeddingCache> _mockEmbeddingCache;
    private readonly Mock<ITextCache> _mockTextCache;
    private readonly Mock<ITagRepository> _mockTagRepository;
    private readonly Mock<ILLMProvider> _mockLlmProvider;
    private readonly Mock<ILLMRateLimiter> _mockRateLimiter;
    private readonly Mock<ICostManager> _mockCostManager;
    private readonly Mock<ILogger<TaggingAgent>> _mockLogger;
    private readonly TaggingAgent _agent;

    public TaggingAgentTests()
    {
        _mockEmbeddingProvider = new Mock<IEmbeddingProvider>();
        _mockVectorStore = new Mock<IVectorStore>();
        _mockEmbeddingCache = new Mock<IEmbeddingCache>();
        _mockTextCache = new Mock<ITextCache>();
        _mockTagRepository = new Mock<ITagRepository>();
        _mockLlmProvider = new Mock<ILLMProvider>();
        _mockRateLimiter = new Mock<ILLMRateLimiter>();
        _mockCostManager = new Mock<ICostManager>();
        _mockLogger = new Mock<ILogger<TaggingAgent>>();

        _agent = new TaggingAgent(
            _mockEmbeddingProvider.Object,
            _mockVectorStore.Object,
            _mockEmbeddingCache.Object,
            _mockTextCache.Object,
            _mockTagRepository.Object,
            _mockLlmProvider.Object,
            _mockRateLimiter.Object,
            _mockCostManager.Object,
            Options.Create(CreateLlmSettings()),
            _mockLogger.Object);

        _mockEmbeddingCache
            .Setup(x => x.TryGetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((float[]?)null);

        _mockEmbeddingCache
            .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockTextCache
            .Setup(x => x.TryGetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        _mockTextCache
            .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockVectorStore
            .Setup(x => x.SearchSimilarAsync(It.IsAny<EmbeddingSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmbeddingSearchResult>());

        _mockTagRepository
            .Setup(x => x.ListByTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Tag>());

        _mockRateLimiter
            .Setup(x => x.ExecuteAsync(It.IsAny<Guid>(), It.IsAny<Func<CancellationToken, Task<LLMResponse>>>(), It.IsAny<CancellationToken>()))
            .Returns<Guid, Func<CancellationToken, Task<LLMResponse>>, CancellationToken>((_, func, ct) => func(ct));

        _mockLlmProvider
            .Setup(x => x.CallAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyDictionary<string, string>?>()))
            .ReturnsAsync(new LLMResponse("[]", "gpt-4", "test", 5, 0.0001m, "completed", 0));
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
    public async Task ExecuteAsync_ShouldReturnSuggestionsFromVectorStore()
    {
        // Arrange
        var context = CreateTestContext("Article about machine learning and neural networks");
        var tenantId = context.TenantId;

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(true, 0.01m, 100m, "BudgetAvailable", "Budget available."));

        _mockEmbeddingProvider
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResponse(new float[] { 0.1f, 0.2f }, "model", "openai", 10, 0.0001m));

        var tagId = Guid.NewGuid();
        _mockVectorStore
            .Setup(x => x.SearchSimilarAsync(It.IsAny<EmbeddingSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmbeddingSearchResult>
            {
                new EmbeddingSearchResult(tagId, 0.1, "{}")
            });

        _mockTagRepository
            .Setup(x => x.ListByIdsAsync(tenantId, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Tag> { CreateTag(tenantId, tagId, "machine-learning") });

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
        var tenantId = context.TenantId;

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(true, 0.01m, 100m, "BudgetAvailable", "Budget available."));

        _mockEmbeddingProvider
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResponse(new float[] { 0.1f }, "model", "openai", 5, 0.0001m));

        var tagId = Guid.NewGuid();
        _mockVectorStore
            .Setup(x => x.SearchSimilarAsync(It.IsAny<EmbeddingSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmbeddingSearchResult>
            {
                new EmbeddingSearchResult(tagId, 0.1, "{}"),
                new EmbeddingSearchResult(tagId, 0.2, "{}")
            });

        _mockTagRepository
            .Setup(x => x.ListByIdsAsync(tenantId, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Tag> { CreateTag(tenantId, tagId, "technology") });

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

        _mockEmbeddingProvider
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResponse(new float[] { 0.1f, 0.2f }, "model", "openai", 10, 0.0001m));

        // Act
        var result = await _agent.ExecuteAsync(context);

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmbeddingFailure_ShouldHandleGracefully()
    {
        // Arrange
        var context = CreateTestContext("Content to tag");

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(true, 0.01m, 100m, "BudgetAvailable", "Budget available."));

        _mockEmbeddingProvider
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Embedding service down"));

        // Act
        var result = await _agent.ExecuteAsync(context);

        // Assert
        Assert.True(result.Success);
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
        _mockEmbeddingProvider.Verify(
            x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithCacheHit_ShouldReturnCachedTags()
    {
        // Arrange
        var context = CreateTestContext("Content for cache");

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(true, 0.01m, 100m, "BudgetAvailable", "Budget available."));

        var cached = new List<TagSuggestionResult>
        {
            new(Guid.Empty, "cache-tag", 0.8)
        };

        _mockTextCache
            .Setup(x => x.TryGetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(System.Text.Json.JsonSerializer.Serialize(cached));

        // Act
        var result = await _agent.ExecuteAsync(context);

        // Assert
        Assert.True(result.Success);
        _mockVectorStore.Verify(
            x => x.SearchSimilarAsync(It.IsAny<EmbeddingSearchRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenVectorStoreReturnsNoMatches_ShouldUseLlmFallback()
    {
        // Arrange
        var context = CreateTestContext("Content without known tags");

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(true, 0.01m, 100m, "BudgetAvailable", "Budget available."));

        _mockEmbeddingProvider
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResponse(new float[] { 0.1f }, "model", "openai", 5, 0.0001m));

        _mockLlmProvider
            .Setup(x => x.CallAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyDictionary<string, string>?>()))
            .ReturnsAsync(new LLMResponse("[\"llm-tag\",\"another\"]", "gpt-4", "test", 12, 0.0002m, "completed", 0));

        // Act
        var result = await _agent.ExecuteAsync(context);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("llm-tag", (List<string>)result.Data.Payload["tags"]);
        _mockRateLimiter.Verify(
            x => x.ExecuteAsync(It.IsAny<Guid>(), It.IsAny<Func<CancellationToken, Task<LLMResponse>>>(), It.IsAny<CancellationToken>()),
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

    private static Tag CreateTag(Guid tenantId, Guid tagId, string name)
    {
        var tag = Tag.Create(tenantId, name, Guid.NewGuid());
        typeof(Tag).GetProperty("Id")!.SetValue(tag, tagId);
        return tag;
    }

    private static AgentLlmSettings CreateLlmSettings()
    {
        return new AgentLlmSettings
        {
            Agents = new Dictionary<string, AgentLlmAgentSettings>(StringComparer.OrdinalIgnoreCase)
            {
                ["TaggingAgent"] = new AgentLlmAgentSettings
                {
                    Chat = new LlmEndpointSettings { Provider = "OpenAI", Model = "gpt-4" },
                    Embedding = new LlmEndpointSettings { Provider = "OpenAI", Model = "text-embedding-3-large" }
                }
            }
        };
    }
}
