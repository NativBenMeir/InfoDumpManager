using System.Diagnostics.CodeAnalysis;
using InfoDumpManager.Application.Agents;
using Xunit;

namespace InfoDumpManager.Tests.Unit.AIAgents;

[ExcludeFromCodeCoverage]
public sealed class AgentContractsTests
{
    [Fact]
    public void AgentContext_ShouldStoreValuesCorrectly()
    {
        // Arrange
        var gemId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var contentText = "Sample content";
        var metadata = new AgentContextMetadata(
            "web",
            42,
            DateTimeOffset.UtcNow,
            new Dictionary<string, object> { { "lang", "en" } });

        // Act
        var context = new AgentContext(gemId, tenantId, contentText, metadata);

        // Assert
        Assert.Equal(gemId, context.GEMId);
        Assert.Equal(tenantId, context.TenantId);
        Assert.Equal(contentText, context.ContentText);
        Assert.Equal(metadata, context.Metadata);
    }

    [Fact]
    public void AgentResult_ShouldStoreMetricsAndConfidence()
    {
        // Arrange
        var data = new AgentResultData(
            "TestAgent",
            DateTimeOffset.UtcNow,
            new Dictionary<string, object> { { "key", "value" } });
        var metrics = new AgentMetrics(10, 0.01m, TimeSpan.FromMilliseconds(50), 1, "provider");
        var confidence = new AgentResultConfidence(0.9, false, "High confidence");

        // Act
        var result = new AgentResult(
            true,
            "ok",
            data,
            metrics,
            null,
            confidence);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("ok", result.Message);
        Assert.Equal(data, result.Data);
        Assert.Equal(metrics, result.Metrics);
        Assert.Equal(confidence, result.Confidence);
    }

    [Fact]
    public void AgentCapability_ShouldContainExpectedValues()
    {
        // Assert
        Assert.Contains(AgentCapability.Summarization, Enum.GetValues<AgentCapability>());
        Assert.Contains(AgentCapability.Categorization, Enum.GetValues<AgentCapability>());
        Assert.Contains(AgentCapability.Tagging, Enum.GetValues<AgentCapability>());
        Assert.Contains(AgentCapability.Validation, Enum.GetValues<AgentCapability>());
        Assert.Contains(AgentCapability.CostManagement, Enum.GetValues<AgentCapability>());
        Assert.Contains(AgentCapability.Orchestration, Enum.GetValues<AgentCapability>());
    }
}
