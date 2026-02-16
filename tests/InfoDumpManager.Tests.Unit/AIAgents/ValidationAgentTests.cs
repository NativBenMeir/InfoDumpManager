using System.Diagnostics.CodeAnalysis;
using InfoDumpManager.Application.Agents;
using InfoDumpManager.Application.Agents.Implementations;
using InfoDumpManager.Application.Services.CostManagement;
using InfoDumpManager.Application.Services.LLM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace InfoDumpManager.Tests.Unit.AIAgents;

[ExcludeFromCodeCoverage]
public sealed class ValidationAgentTests
{
    private readonly Mock<ILLMProvider> _mockLlmProvider;
    private readonly Mock<ILLMRateLimiter> _mockRateLimiter;
    private readonly Mock<ICostManager> _mockCostManager;
    private readonly Mock<ILogger<ValidationAgent>> _mockLogger;
    private readonly ValidationAgent _agent;

    public ValidationAgentTests()
    {
        _mockLlmProvider = new Mock<ILLMProvider>();
        _mockRateLimiter = new Mock<ILLMRateLimiter>();
        _mockCostManager = new Mock<ICostManager>();
        _mockLogger = new Mock<ILogger<ValidationAgent>>();
        _agent = new ValidationAgent(
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
        Assert.Equal("ValidationAgent", _agent.Name);
    }

    [Fact]
    public void Capability_ShouldReturnValidation()
    {
        // Assert
        Assert.Equal(AgentCapability.Validation, _agent.Capability);
    }

    [Fact]
    public async Task ExecuteAsync_WithQualityContent_ShouldPassValidation()
    {
        // Arrange
        var context = CreateTestContext("This is a well-written, coherent article with sufficient length and proper structure.");

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(true, 0.01m, 100m, "BudgetAvailable", "Budget available."));

        _mockLlmProvider
            .Setup(x => x.CallAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyDictionary<string, string>?>()))
            .ReturnsAsync(new LLMResponse("PASS: Content meets quality standards", "gpt-4", "test", 25, 0.001m, "completed", 0));

        // Act
        var result = await _agent.ExecuteAsync(context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Confidence);
        Assert.True(result.Confidence.Score > 0.7);
        Assert.False(result.Confidence.RequiresManualReview);
    }

    [Fact]
    public async Task ExecuteAsync_WithLowQualityContent_ShouldFailValidation()
    {
        // Arrange
        var context = CreateTestContext("bad");

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(true, 0.01m, 100m, "BudgetAvailable", "Budget available."));

        _mockLlmProvider
            .Setup(x => x.CallAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyDictionary<string, string>?>()))
            .ReturnsAsync(new LLMResponse("FAIL: Content too short and lacks coherence", "gpt-4", "test", 20, 0.001m, "completed", 0));

        // Act
        var result = await _agent.ExecuteAsync(context);

        // Assert
        Assert.True(result.Success); // Validation runs successfully even if content fails
        Assert.NotNull(result.Confidence);
        Assert.True(result.Confidence.Score < 0.5 || result.Confidence.RequiresManualReview);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldProvideConfidenceScoring()
    {
        // Arrange
        var context = CreateTestContext("Moderate quality content that might need review.");

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(true, 0.01m, 100m, "BudgetAvailable", "Budget available."));

        _mockLlmProvider
            .Setup(x => x.CallAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyDictionary<string, string>?>()))
            .ReturnsAsync(new LLMResponse("PARTIAL: Some quality issues detected", "gpt-4", "test", 30, 0.001m, "completed", 0));

        // Act
        var result = await _agent.ExecuteAsync(context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Confidence);
        Assert.InRange(result.Confidence.Score, 0.0, 1.0);
    }

    [Theory]
    [InlineData("Short")]
    [InlineData("This is a sentence that is too short for quality content")]
    public async Task ExecuteAsync_ShouldValidateLengthRequirements(string content)
    {
        // Arrange
        var context = CreateTestContext(content);

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(true, 0.01m, 100m, "BudgetAvailable", "Budget available."));

        _mockLlmProvider
            .Setup(x => x.CallAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyDictionary<string, string>?>()))
            .ReturnsAsync(new LLMResponse("FAIL: Insufficient length", "gpt-4", "test", 15, 0.001m, "completed", 0));

        // Act
        var result = await _agent.ExecuteAsync(context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data.Payload);
    }

    [Fact]
    public async Task ExecuteAsync_WithCoherenceIssues_ShouldDetect()
    {
        // Arrange
        var context = CreateTestContext("Random words that make no sense together pizza umbrella quantum fiscal.");

        _mockCostManager
            .Setup(x => x.CanProcessAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostCheckResult(true, 0.01m, 100m, "BudgetAvailable", "Budget available."));

        _mockLlmProvider
            .Setup(x => x.CallAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyDictionary<string, string>?>()))
            .ReturnsAsync(new LLMResponse("FAIL: Lacks coherence and logical flow", "gpt-4", "test", 25, 0.001m, "completed", 0));

        // Act
        var result = await _agent.ExecuteAsync(context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Confidence);
        Assert.Contains("coherence", result.Confidence.Reasoning.ToLowerInvariant());
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

    private static AgentLlmSettings CreateLlmSettings()
    {
        return new AgentLlmSettings
        {
            Agents = new Dictionary<string, AgentLlmAgentSettings>(StringComparer.OrdinalIgnoreCase)
            {
                ["ValidationAgent"] = new AgentLlmAgentSettings
                {
                    Chat = new LlmEndpointSettings { Provider = "OpenAI", Model = "gpt-4" },
                    Embedding = new LlmEndpointSettings { Provider = "OpenAI", Model = "text-embedding-3-large" }
                }
            }
        };
    }
}
