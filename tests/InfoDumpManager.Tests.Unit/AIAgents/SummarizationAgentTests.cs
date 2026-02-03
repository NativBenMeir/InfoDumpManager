using System.Diagnostics.CodeAnalysis;
using InfoDumpManager.Application.Agents;
using InfoDumpManager.Application.Agents.Implementations;
using InfoDumpManager.Application.Services.CostManagement;
using InfoDumpManager.Application.Services.LLM;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace InfoDumpManager.Tests.Unit.AIAgents;

[ExcludeFromCodeCoverage]
public sealed class SummarizationAgentTests
{
    private readonly Mock<ILLMProvider> _mockLlmProvider;
    private readonly Mock<ICostManager> _mockCostManager;
    private readonly Mock<ILogger<SummarizationAgent>> _mockLogger;
    private readonly SummarizationAgent _agent;

    public SummarizationAgentTests()
    {
        _mockLlmProvider = new Mock<ILLMProvider>();
        _mockCostManager = new Mock<ICostManager>();
        _mockLogger = new Mock<ILogger<SummarizationAgent>>();
        _agent = new SummarizationAgent(_mockLlmProvider.Object, _mockCostManager.Object, _mockLogger.Object);
    }

    [Fact]
    public void Name_ShouldReturnCorrectValue()
    {
        // Assert
        Assert.Equal("SummarizationAgent", _agent.Name);
    }

    [Fact]
    public void Capability_ShouldReturnSummarization()
    {
        // Assert
        Assert.Equal(AgentCapability.Summarization, _agent.Capability);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidContent_ShouldReturnSuccessResult()
    {
        // Arrange
        var context = CreateTestContext("This is a long content that needs summarization.");
        var expectedSummary = "Summarized content";

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(true, 0.01m, 100m, "BudgetAvailable", "Budget available."));

        _mockLlmProvider
            .Setup(x => x.CallAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse(expectedSummary, "gpt-4", "test-provider", 50, 0.001m, "completed", 0));

        // Act
        var result = await _agent.ExecuteAsync(context);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("summary", result.Message.ToLowerInvariant());
        Assert.Equal("SummarizationAgent", result.Data.AgentName);
        Assert.Equal(50, result.Metrics.TokensUsed);
        Assert.Equal(0.001m, result.Metrics.EstimatedCost);
    }

    [Fact]
    public async Task ExecuteAsync_WithCostBudgetDenied_ShouldReturnFailureWithoutCallingProvider()
    {
        // Arrange
        var context = CreateTestContext("Content to summarize");

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(false, 0m, 0m, "BudgetExceeded", "Budget exceeded"));

        // Act
        var result = await _agent.ExecuteAsync(context);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("budget", result.Message.ToLowerInvariant());
        _mockLlmProvider.Verify(
            x => x.CallAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithLLMFailure_ShouldReturnFailureResult()
    {
        // Arrange
        var context = CreateTestContext("Content to summarize");

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(true, 0.01m, 100m, "BudgetAvailable", "Budget available."));

        _mockLlmProvider
            .Setup(x => x.CallAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM service unavailable"));

        // Act
        var result = await _agent.ExecuteAsync(context);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Errors);
        Assert.Contains(result.Errors, e => e.Contains("LLM service unavailable"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task SummarizeAsync_WithEmptyContent_ShouldThrowArgumentException(string? content)
    {
        // Arrange
        var options = new SummarizationOptions();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _agent.SummarizeAsync(content!, options));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldTrackTokenCountAccurately()
    {
        // Arrange
        var context = CreateTestContext("Content with specific token count");
        var expectedTokens = 42;

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(true, 0.01m, 100m, "BudgetAvailable", "Budget available."));

        _mockLlmProvider
            .Setup(x => x.CallAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse("Summary", "gpt-4", "test", expectedTokens, 0.005m, "completed", 0));

        // Act
        var result = await _agent.ExecuteAsync(context);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expectedTokens, result.Metrics.TokensUsed);
        Assert.Equal(0.005m, result.Metrics.EstimatedCost);
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
