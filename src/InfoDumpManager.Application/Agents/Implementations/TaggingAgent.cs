using System.Diagnostics;
using InfoDumpManager.Application.Services.CostManagement;
using InfoDumpManager.Application.Services.Embeddings;
using InfoDumpManager.Application.Services.LLM;
using Microsoft.Extensions.Logging;

namespace InfoDumpManager.Application.Agents.Implementations;

public sealed class TaggingAgent : IAgent
{
    private const string OperationName = "tagging";

    private readonly ILLMProvider _llmProvider;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IVectorStore _vectorStore;
    private readonly ICostManager _costManager;
    private readonly ILogger<TaggingAgent> _logger;

    public TaggingAgent(
        ILLMProvider llmProvider,
        IEmbeddingProvider embeddingProvider,
        IVectorStore vectorStore,
        ICostManager costManager,
        ILogger<TaggingAgent> logger)
    {
        _llmProvider = llmProvider;
        _embeddingProvider = embeddingProvider;
        _vectorStore = vectorStore;
        _costManager = costManager;
        _logger = logger;
    }

    public string Name => "TaggingAgent";

    public AgentCapability Capability => AgentCapability.Tagging;

    public async Task<AgentResult> ExecuteAsync(AgentContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var budgetCheck = await _costManager
                .CanProcessAsync(context.TenantId, context.Metadata.EstimatedTokenCount, OperationName)
                .ConfigureAwait(false);

            if (!budgetCheck.Allowed)
            {
                return BuildFailure(context, budgetCheck.Message, stopwatch.Elapsed);
            }

            var tags = await GenerateTagsAsync(context.ContentText).ConfigureAwait(false);
            var embedding = await _embeddingProvider
                .GenerateEmbeddingAsync(context.ContentText, "text-embedding-3-large")
                .ConfigureAwait(false);

            await _costManager.RecordUsageAsync(
                context.TenantId,
                context.GEMId,
                OperationName,
                embedding.TokensUsed,
                embedding.CostEstimate)
                .ConfigureAwait(false);

            var record = new EmbeddingRecord(
                Guid.NewGuid(),
                context.TenantId,
                context.GEMId,
                "gem",
                embedding.Model,
                embedding.Vector,
                string.Join(',', tags),
                DateTimeOffset.UtcNow);

            await _vectorStore.StoreAsync(record).ConfigureAwait(false);
            stopwatch.Stop();

            _logger.LogInformation(
                "Tagging completed for GEM {GemId}. Tokens {TokensUsed}, Cost {Cost}, DurationMs {DurationMs}, Tags {TagCount}",
                context.GEMId,
                embedding.TokensUsed,
                embedding.CostEstimate,
                stopwatch.ElapsedMilliseconds,
                tags.Count);

            return new AgentResult(
                true,
                "Tagging completed",
                new AgentResultData(
                    Name,
                    DateTimeOffset.UtcNow,
                    new Dictionary<string, object>
                    {
                        { "tags", tags }
                    }),
                new AgentMetrics(
                    embedding.TokensUsed,
                    embedding.CostEstimate,
                    stopwatch.Elapsed,
                    0,
                    embedding.Provider));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tagging failed for GEM {GemId}", context.GEMId);
            return BuildFailure(context, ex.Message, stopwatch.Elapsed);
        }
    }

    public async Task<TaggingResult> TagAsync(string content)
    {
        var tags = await GenerateTagsAsync(content).ConfigureAwait(false);
        return new TaggingResult(tags, DateTimeOffset.UtcNow);
    }

    private async Task<List<string>> GenerateTagsAsync(string content)
    {
        var prompt = $"Generate 5-8 concise tags for the following content. Return as comma-separated list.\n\n{content}";
        var response = await _llmProvider.CallAsync(prompt, "gpt-4", 80, 0.4f).ConfigureAwait(false);
        return response.Content
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private AgentResult BuildFailure(AgentContext context, string message, TimeSpan duration)
    {
        return new AgentResult(
            false,
            "Tagging failed",
            new AgentResultData(
                Name,
                DateTimeOffset.UtcNow,
                new Dictionary<string, object>
                {
                    { "message", message }
                }),
            new AgentMetrics(0, 0m, duration, 0, "unknown"),
            new List<string> { message },
            new AgentResultConfidence(0.2, true, "Tagging failed"));
    }
}

public sealed record TaggingResult(
    IReadOnlyList<string> Tags,
    DateTimeOffset GeneratedAt);
