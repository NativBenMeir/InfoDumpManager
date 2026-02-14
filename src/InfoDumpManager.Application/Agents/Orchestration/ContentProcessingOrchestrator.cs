using InfoDumpManager.Application.Agents;
using InfoDumpManager.Application.Common.Events;
using InfoDumpManager.Domain.Events;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InfoDumpManager.Application.Agents.Orchestration;

/// <summary>
/// Coordinates multi-agent processing for GEM content.
/// Delegates persistence and activity logging to dedicated services.
/// </summary>
public sealed class ContentProcessingOrchestrator : IContentProcessingOrchestrator
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IJobTracker _jobTracker;
    private readonly ILogger<ContentProcessingOrchestrator> _logger;

    public ContentProcessingOrchestrator(
        IServiceScopeFactory scopeFactory,
        IJobTracker jobTracker,
        ILogger<ContentProcessingOrchestrator> logger)
    {
        _scopeFactory = scopeFactory;
        _jobTracker = jobTracker;
        _logger = logger;
    }

    public async Task<ProcessingResult> ProcessGEMAsync(
        Guid gemId,
        Guid tenantId,
        string contentText,
        ProcessingOptions options,
        Guid? jobId = null)
    {
        var resolvedJobId = jobId ?? Guid.NewGuid();
        var errors = new List<string>();
        AgentResult? summarization = null;
        AgentResult? categorization = null;
        AgentResult? tagging = null;
        AgentResult? validation = null;
        GEMSummary? summary = null;

        _jobTracker.UpdateStatus(resolvedJobId, ProcessingStatus.Processing, 0, "Starting processing");

        await using var scope = _scopeFactory.CreateAsyncScope();
        var agents = scope.ServiceProvider.GetServices<IAgent>().ToList();
        var agentMap = agents
            .GroupBy(agent => agent.Capability)
            .ToDictionary(group => group.Key, group => group.First());
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var persistence = scope.ServiceProvider.GetRequiredService<IProcessingPersistence>();
        var activityLogger = scope.ServiceProvider.GetRequiredService<IProcessingActivityLogger>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // Enrich content with GEM title if only raw content was provided
        var enrichedContent = contentText;
        if (!string.IsNullOrWhiteSpace(contentText))
        {
            var gem = await unitOfWork.GEMs.GetByIdAsync(gemId).ConfigureAwait(false);
            if (gem is not null && !contentText.StartsWith($"Title: {gem.Title}"))
            {
                enrichedContent = $"Title: {gem.Title}\n\n{contentText}";
            }
        }

        try
        {
            if (options.RunValidation)
            {
                var validationAgent = ResolveAgent(agentMap, AgentCapability.Validation, errors);
                if (validationAgent is null)
                {
                    _jobTracker.UpdateStatus(resolvedJobId, ProcessingStatus.Processing, 10, "Validation skipped (agent unavailable)");
                }
                else
                {
                    validation = await validationAgent.ExecuteAsync(CreateContext(gemId, tenantId, enrichedContent, options));

                    if (!validation.Success)
                    {
                        errors.AddRange(validation.Errors ?? new());
                        return CreateFailedResult(resolvedJobId, gemId, summary, summarization, categorization, tagging, validation, errors);
                    }

                    await activityLogger.LogValidationAsync(tenantId, gemId, validation).ConfigureAwait(false);
                    _jobTracker.UpdateStatus(resolvedJobId, ProcessingStatus.Processing, 15, "Validation complete");
                }
            }

            var summarizationAgent = ResolveAgent(agentMap, AgentCapability.Summarization, errors);
            if (summarizationAgent is null)
            {
                return CreateFailedResult(resolvedJobId, gemId, summary, summarization, categorization, tagging, validation, errors);
            }

            summarization = await summarizationAgent.ExecuteAsync(CreateContext(gemId, tenantId, enrichedContent, options));

            if (!summarization.Success)
            {
                errors.AddRange(summarization.Errors ?? new());
                return CreateFailedResult(resolvedJobId, gemId, summary, summarization, categorization, tagging, validation, errors);
            }

            summary = TryBuildSummary(summarization);
            await persistence.PersistSummaryAsync(gemId, summary).ConfigureAwait(false);
            await activityLogger.LogSummarizationAsync(tenantId, gemId, summarization).ConfigureAwait(false);
            await mediator.Publish(new DomainEventNotification(new GEMSummarizationCompleted(
                gemId,
                tenantId,
                summary?.Text ?? string.Empty,
                summary?.TokenCount ?? 0,
                DateTimeOffset.UtcNow))).ConfigureAwait(false);
            _jobTracker.UpdateStatus(resolvedJobId, ProcessingStatus.Processing, 25, "Summarization complete");

            var categorizationAgent = ResolveAgent(agentMap, AgentCapability.Categorization, errors);
            if (categorizationAgent is not null)
            {
                var categories = await unitOfWork.Categories.ListByTenantAsync(tenantId).ConfigureAwait(false);
                var categorizationContext = CreateContext(gemId, tenantId, enrichedContent, options);
                categorizationContext.Metadata.CustomData["categories"] = categories;
                categorization = await categorizationAgent.ExecuteAsync(categorizationContext);

                if (!categorization.Success)
                {
                    errors.AddRange(categorization.Errors ?? new());
                }
                else
                {
                    await persistence.HandleCategorizationAsync(tenantId, gemId, categorization, options)
                        .ConfigureAwait(false);
                }
            }

            _jobTracker.UpdateStatus(resolvedJobId, ProcessingStatus.Processing, 50, "Categorization complete");

            var taggingAgent = ResolveAgent(agentMap, AgentCapability.Tagging, errors);
            if (taggingAgent is not null)
            {
                tagging = await taggingAgent.ExecuteAsync(CreateContext(gemId, tenantId, enrichedContent, options));

                if (!tagging.Success)
                {
                    errors.AddRange(tagging.Errors ?? new());
                }
                else
                {
                    await persistence.HandleTaggingAsync(tenantId, gemId, tagging).ConfigureAwait(false);
                }
            }

            _jobTracker.UpdateStatus(resolvedJobId, ProcessingStatus.Processing, 75, "Tagging complete");

            _jobTracker.UpdateStatus(resolvedJobId, ProcessingStatus.Completed, 100, "Processing complete");

            return new ProcessingResult(
                gemId,
                ProcessingStatus.Completed,
                summary,
                summarization,
                categorization,
                tagging,
                validation,
                errors,
                DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Processing failed for GEM {GemId}", gemId);
            errors.Add(ex.Message);
            return CreateFailedResult(resolvedJobId, gemId, summary, summarization, categorization, tagging, validation, errors);
        }
    }

    public async Task<ProcessingResult> ProcessBatchAsync(
        IEnumerable<(Guid GEMId, Guid TenantId, string ContentText)> items,
        ProcessingOptions options)
    {
        var itemList = items.ToList();
        var concurrencyLimit = options.MaxConcurrentJobs ?? 3;
        var semaphore = new SemaphoreSlim(concurrencyLimit);

        var tasks = itemList.Select(async item =>
        {
            await semaphore.WaitAsync();
            try
            {
                return await ProcessGEMAsync(item.GEMId, item.TenantId, item.ContentText, options);
            }
            finally
            {
                semaphore.Release();
            }
        }).ToList();

        var results = await Task.WhenAll(tasks);

        var batchErrors = results.SelectMany(result => result.Errors).ToList();

        return new ProcessingResult(
            Guid.Empty,
            batchErrors.Count == 0 ? ProcessingStatus.Completed : ProcessingStatus.Failed,
            null,
            null,
            null,
            null,
            null,
            batchErrors,
            DateTimeOffset.UtcNow);
    }

    public Task<JobStatus> GetJobStatusAsync(Guid jobId)
        => _jobTracker.GetJobStatusAsync(jobId);

    public IAsyncEnumerable<JobStatusUpdate> WatchJobAsync(Guid jobId)
        => _jobTracker.WatchJobAsync(jobId);

    private AgentContext CreateContext(Guid gemId, Guid tenantId, string contentText, ProcessingOptions options)
    {
        var metadata = new AgentContextMetadata(
            options.Source,
            EstimateTokens(contentText),
            DateTimeOffset.UtcNow,
            new Dictionary<string, object>());

        return new AgentContext(gemId, tenantId, contentText, metadata);
    }

    private static IAgent? ResolveAgent(
        IReadOnlyDictionary<AgentCapability, IAgent> agents,
        AgentCapability capability,
        List<string> errors)
    {
        if (agents.TryGetValue(capability, out var agent))
        {
            return agent;
        }

        errors.Add($"Missing agent for capability '{capability}'.");
        return null;
    }

    private static int EstimateTokens(string text)
        => (int)(text.Length / 4.0);

    private static GEMSummary? TryBuildSummary(AgentResult result)
    {
        if (result.Data.Payload.TryGetValue("summaryObject", out var summaryObject)
            && summaryObject is GEMSummary existingSummary)
        {
            return existingSummary;
        }

        if (result.Data.Payload.TryGetValue("summary", out var summaryObj)
            && summaryObj is string summaryText
            && !string.IsNullOrWhiteSpace(summaryText))
        {
            var model = result.Data.Payload.TryGetValue("model", out var modelObj) && modelObj is string modelText
                ? modelText
                : "unknown";
            var tokenCount = result.Data.Payload.TryGetValue("tokenCount", out var tokenObj) && tokenObj is int tokens
                ? tokens
                : 0;

            return GEMSummary.Create(summaryText, model, tokenCount, DateTimeOffset.UtcNow);
        }

        return null;
    }

    private ProcessingResult CreateFailedResult(
        Guid jobId,
        Guid gemId,
        GEMSummary? summary,
        AgentResult? summarization,
        AgentResult? categorization,
        AgentResult? tagging,
        AgentResult? validation,
        List<string> errors)
    {
        _jobTracker.UpdateStatus(jobId, ProcessingStatus.Failed, 100, "Processing failed");

        return new ProcessingResult(
            gemId,
            ProcessingStatus.Failed,
            summary,
            summarization,
            categorization,
            tagging,
            validation,
            errors,
            DateTimeOffset.UtcNow);
    }
}
