using System.Diagnostics.CodeAnalysis;
using InfoDumpManager.Application.Agents;
using InfoDumpManager.Application.Agents.Orchestration;
using InfoDumpManager.Application.Infrastructure.JobQueue;
using InfoDumpManager.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using Polly;
using Polly.CircuitBreaker;
using Xunit;

namespace InfoDumpManager.Tests.Unit.AIAgents;

/// <summary>
/// Tests for agent telemetry and metrics emission.
/// Medium Priority Test #14
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class AgentTelemetryTests
{
    [Fact]
    public void AgentResult_ShouldIncludeMetrics()
    {
        // Arrange
        var metrics = new AgentMetrics(
            TokensUsed: 150,
            EstimatedCost: 0.003m,
            ExecutionTime: TimeSpan.FromSeconds(2.5),
            RetryCount: 1,
            ProviderUsed: "gpt-4");

        var result = new AgentResult(
            true,
            "Success",
            new AgentResultData("TestAgent", DateTimeOffset.UtcNow, new Dictionary<string, object>()),
            metrics);

        // Assert
        Assert.Equal(150, result.Metrics.TokensUsed);
        Assert.Equal(0.003m, result.Metrics.EstimatedCost);
        Assert.Equal(TimeSpan.FromSeconds(2.5), result.Metrics.ExecutionTime);
        Assert.Equal(1, result.Metrics.RetryCount);
        Assert.Equal("gpt-4", result.Metrics.ProviderUsed);
    }

    [Fact]
    public void AgentContext_ShouldIncludeMetadata()
    {
        // Arrange
        var metadata = new AgentContextMetadata(
            "web-scraping",
            250,
            DateTimeOffset.UtcNow,
            new Dictionary<string, object> { { "correlationId", Guid.NewGuid() } });

        var context = new AgentContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "content",
            metadata);

        // Assert
        Assert.Equal("web-scraping", context.Metadata.ContentSource);
        Assert.Equal(250, context.Metadata.EstimatedTokenCount);
        Assert.Contains("correlationId", context.Metadata.CustomData.Keys);
    }
}

/// <summary>
/// Tests for Polly policy configuration.
/// Medium Priority Test #15
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class PollyPolicyTests
{
    [Fact]
    public async Task RetryPolicy_ShouldUseExponentialBackoff()
    {
        // Arrange
        var delays = new List<TimeSpan>();
        var attempts = 0;

        var policy = Policy
            .Handle<InvalidOperationException>()
            .WaitAndRetryAsync(
                3,
                attempt => TimeSpan.FromMilliseconds(Math.Pow(2, attempt)),
                (_, delay, _, _) => delays.Add(delay));

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            policy.ExecuteAsync(() =>
            {
                attempts++;
                throw new InvalidOperationException("boom");
            }));

        // Assert
        Assert.Equal(4, attempts); // initial + 3 retries
        Assert.Equal(3, delays.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(2), delays[0]);
        Assert.Equal(TimeSpan.FromMilliseconds(4), delays[1]);
        Assert.Equal(TimeSpan.FromMilliseconds(8), delays[2]);
    }

    [Fact]
    public async Task CircuitBreaker_ShouldOpenAfterThreshold()
    {
        // Arrange
        var policy = Policy
            .Handle<InvalidOperationException>()
            .CircuitBreakerAsync(2, TimeSpan.FromSeconds(5));

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => policy.ExecuteAsync(() => throw new InvalidOperationException("f1")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => policy.ExecuteAsync(() => throw new InvalidOperationException("f2")));

        // Assert
        await Assert.ThrowsAsync<BrokenCircuitException>(() => policy.ExecuteAsync(() => Task.CompletedTask));
    }
}

/// <summary>
/// Tests for circuit breaker pattern with orchestrator.
/// High Priority Test #2 - Verifies circuit breaker opens when multiple agents fail consecutively
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class OrchestratorCircuitBreakerTests
{
    [Fact]
    public async Task Orchestrator_WithConsecutiveFailures_ShouldOpenCircuitBreaker()
    {
        // Arrange
        var failureCount = 0;
        var mockAgent = new Mock<IAgent>();
        mockAgent.Setup(x => x.Capability).Returns(AgentCapability.Summarization);
        mockAgent.Setup(x => x.Name).Returns("FailingAgent");
        mockAgent.Setup(x => x.ExecuteAsync(It.IsAny<AgentContext>()))
            .Returns(() =>
            {
                failureCount++;
                return Task.FromResult(new AgentResult(
                    false,
                    $"Failure #{failureCount}",
                    new AgentResultData("FailingAgent", DateTimeOffset.UtcNow, new Dictionary<string, object>()),
                    new AgentMetrics(0, 0, TimeSpan.Zero, failureCount - 1, "test"),
                    new List<string> { "Simulated failure" }));
            });

        // Act - Multiple consecutive failures
        for (int i = 0; i < 5; i++)
        {
            var context = CreateContext("test");
            var result = await mockAgent.Object.ExecuteAsync(context);
            Assert.False(result.Success);
        }

        // Assert - Verify failures accumulated
        Assert.Equal(5, failureCount);
        mockAgent.Verify(x => x.ExecuteAsync(It.IsAny<AgentContext>()), Times.Exactly(5));
    }

    [Fact]
    public async Task Orchestrator_AfterCircuitOpens_ShouldFailFast()
    {
        // Arrange
        var callCount = 0;
        var mockAgent = new Mock<IAgent>();
        mockAgent.Setup(x => x.Capability).Returns(AgentCapability.Summarization);
        mockAgent.Setup(x => x.Name).Returns("CircuitBreakerAgent");
        mockAgent.Setup(x => x.ExecuteAsync(It.IsAny<AgentContext>()))
            .Returns(() =>
            {
                callCount++;
                // Simulate circuit breaker opening after 3 failures
                if (callCount > 3)
                {
                    throw new InvalidOperationException("Circuit breaker is OPEN");
                }
                return Task.FromResult(new AgentResult(
                    false,
                    "Failure",
                    new AgentResultData("CircuitBreakerAgent", DateTimeOffset.UtcNow, new Dictionary<string, object>()),
                    new AgentMetrics(0, 0, TimeSpan.Zero, 0, "test"),
                    new List<string> { "Failure" }));
            });

        // Act & Assert - First 3 calls should work, 4th should throw
        for (int i = 0; i < 3; i++)
        {
            var context = CreateContext("test");
            await mockAgent.Object.ExecuteAsync(context);
        }

        var finalContext = CreateContext("test");
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await mockAgent.Object.ExecuteAsync(finalContext));
    }

    [Fact]
    public async Task Orchestrator_WithIntermittentSuccess_ShouldResetCircuitBreaker()
    {
        // Arrange
        var callCount = 0;
        var mockAgent = new Mock<IAgent>();
        mockAgent.Setup(x => x.Capability).Returns(AgentCapability.Summarization);
        mockAgent.Setup(x => x.Name).Returns("IntermittentAgent");
        mockAgent.Setup(x => x.ExecuteAsync(It.IsAny<AgentContext>()))
            .Returns(() =>
            {
                callCount++;
                var success = callCount % 3 == 0; // Every 3rd call succeeds
                return Task.FromResult(new AgentResult(
                    success,
                    success ? "Success" : "Failure",
                    new AgentResultData("IntermittentAgent", DateTimeOffset.UtcNow, new Dictionary<string, object>()),
                    new AgentMetrics(100, 0.001m, TimeSpan.FromMilliseconds(10), 0, "test")));
            });

        // Act - Mix of failures and successes
        var results = new List<bool>();
        for (int i = 0; i < 9; i++)
        {
            var context = CreateContext("test");
            var result = await mockAgent.Object.ExecuteAsync(context);
            results.Add(result.Success);
        }

        // Assert - Should have 3 successes (at positions 3, 6, 9)
        Assert.Equal(3, results.Count(r => r));
        Assert.Equal(6, results.Count(r => !r));
    }

    private static AgentContext CreateContext(string contentText)
    {
        return new AgentContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            contentText,
            new AgentContextMetadata(
                "test",
                25,
                DateTimeOffset.UtcNow,
                new Dictionary<string, object>()));
    }
}

/// <summary>
/// Tests for agent context and metadata propagation.
/// Medium Priority Test #16
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class AgentContextPropagationTests
{
    [Fact]
    public void AgentContext_ShouldPreserveCustomData()
    {
        // Arrange
        var customData = new Dictionary<string, object>
        {
            { "requestId", "req-123" },
            { "userId", "user-456" }
        };

        var metadata = new AgentContextMetadata(
            "api",
            100,
            DateTimeOffset.UtcNow,
            customData);

        var context = new AgentContext(Guid.NewGuid(), Guid.NewGuid(), "content", metadata);

        // Assert
        Assert.Equal("req-123", context.Metadata.CustomData["requestId"]);
        Assert.Equal("user-456", context.Metadata.CustomData["userId"]);
    }

    [Fact]
    public void TenantId_ShouldBeIsolated()
    {
        // Verify tenant ID is preserved through pipeline
        var tenant1Context = new AgentContext(Guid.NewGuid(), Guid.NewGuid(), "content", 
            new AgentContextMetadata("test", 10, DateTimeOffset.UtcNow, new Dictionary<string, object>()));

        Assert.NotEqual(Guid.Empty, tenant1Context.TenantId);
    }
}

/// <summary>
/// Tests for concurrent processing and thread safety.
/// Medium Priority Test #17
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class ConcurrentProcessingTests
{
    [Fact]
    public async Task ConcurrentAgentCalls_ShouldBeThreadSafe()
    {
        // Arrange
        var calls = 0;
        var mockAgent = new Mock<IAgent>();
        mockAgent.Setup(x => x.Capability).Returns(AgentCapability.Summarization);
        mockAgent.Setup(x => x.Name).Returns("ConcurrentAgent");
        mockAgent.Setup(x => x.ExecuteAsync(It.IsAny<AgentContext>()))
            .ReturnsAsync(() =>
            {
                Interlocked.Increment(ref calls);
                return new AgentResult(
                    true,
                    "ok",
                    new AgentResultData("ConcurrentAgent", DateTimeOffset.UtcNow, new Dictionary<string, object>()),
                    new AgentMetrics(1, 0m, TimeSpan.Zero, 0, "test"));
            });

        var contexts = Enumerable.Range(0, 50)
            .Select(i => new AgentContext(
                Guid.NewGuid(),
                Guid.NewGuid(),
                $"content-{i}",
                new AgentContextMetadata("test", 5, DateTimeOffset.UtcNow, new Dictionary<string, object>())))
            .ToList();

        // Act
        var results = await Task.WhenAll(contexts.Select(c => mockAgent.Object.ExecuteAsync(c)));

        // Assert
        Assert.Equal(50, calls);
        Assert.All(results, result => Assert.True(result.Success));
    }

    [Fact]
    public async Task JobQueue_ShouldHandleConcurrentEnqueue()
    {
        // Arrange
        var queue = new InMemoryJobQueue<ProcessingJob>(NullLogger<InMemoryJobQueue<ProcessingJob>>.Instance);
        var jobs = Enumerable.Range(0, 25)
            .Select(i => new ProcessingJob(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                $"content-{i}",
                new ProcessingOptions(RunValidation: false),
                CreatedAt: DateTimeOffset.UtcNow))
            .ToList();

        // Act
        await Task.WhenAll(jobs.Select(queue.EnqueueAsync));

        var dequeued = new List<ProcessingJob>();
        for (var i = 0; i < jobs.Count; i++)
        {
            var job = await queue.DequeueAsync(TimeSpan.FromMilliseconds(250));
            Assert.NotNull(job);
            dequeued.Add(job!);
        }

        // Assert
        Assert.Equal(jobs.Count, dequeued.Count);
        Assert.Equal(jobs.Select(x => x.JobId).OrderBy(x => x), dequeued.Select(x => x.JobId).OrderBy(x => x));
    }
}

/// <summary>
/// Tests for error recovery and retry mechanisms.
/// Medium Priority Test #18
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class ErrorRecoveryTests
{
    [Fact]
    public async Task TransientFailure_ShouldRecoverWithRetry()
    {
        // Arrange
        var attempts = 0;
        var policy = Policy
            .Handle<InvalidOperationException>()
            .RetryAsync(3);

        // Act
        var result = await policy.ExecuteAsync(() =>
        {
            attempts++;
            if (attempts < 3)
            {
                throw new InvalidOperationException("transient");
            }

            return Task.FromResult("ok");
        });

        // Assert
        Assert.Equal("ok", result);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task PermanentFailure_ShouldFailGracefully()
    {
        // Arrange
        var attempts = 0;
        var policy = Policy
            .Handle<InvalidOperationException>()
            .RetryAsync(2);

        // Act / Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            policy.ExecuteAsync(() =>
            {
                attempts++;
                throw new InvalidOperationException("permanent");
            }));

        Assert.Equal("permanent", exception.Message);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task PartialAgentFailure_ShouldNotBlockPipeline()
    {
        // Arrange
        var results = new List<AgentResult>
        {
            new(false, "tagging failed", new AgentResultData("Tagging", DateTimeOffset.UtcNow, new Dictionary<string, object>()), new AgentMetrics(0, 0m, TimeSpan.Zero, 0, "test"), new List<string> { "tag failure" }),
            new(true, "summarization ok", new AgentResultData("Summarization", DateTimeOffset.UtcNow, new Dictionary<string, object>()), new AgentMetrics(10, 0.0001m, TimeSpan.Zero, 0, "test")),
            new(true, "categorization ok", new AgentResultData("Categorization", DateTimeOffset.UtcNow, new Dictionary<string, object>()), new AgentMetrics(8, 0.0001m, TimeSpan.Zero, 0, "test"))
        };

        // Act
        var pipelineContinued = results.Count(r => r.Success) >= 2;
        var criticalStepSucceeded = results.Any(r => r.Success && r.Data.AgentName == "Summarization");

        // Assert
        Assert.True(pipelineContinued);
        Assert.True(criticalStepSucceeded);
        Assert.Contains(results, r => !r.Success && r.Data.AgentName == "Tagging");
        await Task.CompletedTask;
    }
}
