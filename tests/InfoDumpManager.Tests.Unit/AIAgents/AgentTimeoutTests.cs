using System.Diagnostics.CodeAnalysis;
using InfoDumpManager.Application.Agents;
using InfoDumpManager.Application.Services.LLM;
using Moq;
using Xunit;

namespace InfoDumpManager.Tests.Unit.AIAgents;

/// <summary>
/// Tests for agent timeout and cancellation scenarios.
/// Ensures agents gracefully handle timeouts when LLM provider calls exceed limits.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class AgentTimeoutTests
{
    [Fact]
    public async Task AgentExecuteAsync_WithTimeout_ShouldCancelAndReturnFailure()
    {
        // Arrange
        var mockProvider = new Mock<ILLMProvider>();
        mockProvider
            .Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<LLMOptions>(), It.IsAny<CancellationToken>()))
            .Returns(async (string prompt, LLMOptions options, CancellationToken ct) =>
            {
                // Simulate long-running operation
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
                return new LLMResponse("result", 100, "model", TimeSpan.FromSeconds(10));
            });

        var agent = new TestTimeoutAgent(mockProvider.Object, AgentCapability.Summarization);
        var context = new AgentContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test content",
            new Dictionary<string, object>());

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await agent.ExecuteWithCancellationAsync(context, cts.Token));
    }

    [Fact]
    public async Task AgentExecuteAsync_WhenCancelled_ShouldNotCallProvider()
    {
        // Arrange
        var mockProvider = new Mock<ILLMProvider>();
        var callCount = 0;

        mockProvider
            .Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<LLMOptions>(), It.IsAny<CancellationToken>()))
            .Callback(() => callCount++)
            .ReturnsAsync(new LLMResponse("result", 100, "model", TimeSpan.FromMilliseconds(10)));

        var agent = new TestTimeoutAgent(mockProvider.Object, AgentCapability.Summarization);
        var context = new AgentContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test content",
            new Dictionary<string, object>());

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await agent.ExecuteWithCancellationAsync(context, cts.Token));

        Assert.Equal(0, callCount); // Provider should not be called
    }

    [Fact]
    public async Task AgentExecuteAsync_WithShortTimeout_ShouldRespectCancellationToken()
    {
        // Arrange
        var mockProvider = new Mock<ILLMProvider>();
        mockProvider
            .Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<LLMOptions>(), It.IsAny<CancellationToken>()))
            .Returns(async (string prompt, LLMOptions options, CancellationToken ct) =>
            {
                // Check cancellation token during operation
                for (int i = 0; i < 100; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    await Task.Delay(50, ct);
                }
                return new LLMResponse("result", 100, "model", TimeSpan.FromSeconds(5));
            });

        var agent = new TestTimeoutAgent(mockProvider.Object, AgentCapability.Summarization);
        var context = new AgentContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test content",
            new Dictionary<string, object>());

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await agent.ExecuteWithCancellationAsync(context, cts.Token));
    }

    [Fact]
    public async Task AgentExecuteAsync_WithNormalOperation_ShouldCompleteSuccessfully()
    {
        // Arrange
        var mockProvider = new Mock<ILLMProvider>();
        mockProvider
            .Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<LLMOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse("Summary result", 100, "model", TimeSpan.FromMilliseconds(50)));

        var agent = new TestTimeoutAgent(mockProvider.Object, AgentCapability.Summarization);
        var context = new AgentContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test content",
            new Dictionary<string, object>());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var result = await agent.ExecuteWithCancellationAsync(context, cts.Token);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("Summary result", result.Message);
    }

    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(500)]
    public async Task AgentExecuteAsync_WithVariableTimeouts_ShouldHandleCorrectly(int timeoutMs)
    {
        // Arrange
        var mockProvider = new Mock<ILLMProvider>();
        mockProvider
            .Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<LLMOptions>(), It.IsAny<CancellationToken>()))
            .Returns(async (string prompt, LLMOptions options, CancellationToken ct) =>
            {
                await Task.Delay(timeoutMs + 50, ct); // Longer than timeout
                return new LLMResponse("result", 100, "model", TimeSpan.FromMilliseconds(timeoutMs));
            });

        var agent = new TestTimeoutAgent(mockProvider.Object, AgentCapability.Summarization);
        var context = new AgentContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test content",
            new Dictionary<string, object>());

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await agent.ExecuteWithCancellationAsync(context, cts.Token));
    }

    // Test helper class that supports cancellation
    private sealed class TestTimeoutAgent : IAgent
    {
        private readonly ILLMProvider _provider;

        public TestTimeoutAgent(ILLMProvider provider, AgentCapability capability)
        {
            _provider = provider;
            Capability = capability;
            Name = $"Test{capability}Agent";
        }

        public string Name { get; }
        public AgentCapability Capability { get; }

        public Task<AgentResult> ExecuteAsync(AgentContext context)
        {
            return ExecuteWithCancellationAsync(context, CancellationToken.None);
        }

        public async Task<AgentResult> ExecuteWithCancellationAsync(AgentContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var startTime = DateTimeOffset.UtcNow;

            try
            {
                var response = await _provider.GenerateAsync(
                    $"Process: {context.ContentText}",
                    new LLMOptions("gpt-4", 150, 0.7),
                    cancellationToken);

                var payload = new Dictionary<string, object>
                {
                    ["result"] = response.Content
                };

                return new AgentResult(
                    true,
                    response.Content,
                    new AgentResultData(Name, DateTimeOffset.UtcNow, payload),
                    new AgentMetrics(response.TokenCount, 0.001m, response.Duration, 0, response.Model));
            }
            catch (OperationCanceledException)
            {
                // Re-throw cancellation to be handled by caller
                throw;
            }
            catch (Exception ex)
            {
                return new AgentResult(
                    false,
                    ex.Message,
                    new AgentResultData(Name, DateTimeOffset.UtcNow, new Dictionary<string, object>()),
                    new AgentMetrics(0, 0, DateTimeOffset.UtcNow - startTime, 0, "unknown"),
                    new List<string> { ex.Message });
            }
        }
    }
}
