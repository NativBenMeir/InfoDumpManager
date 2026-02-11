using System.Text.Json;
using InfoDumpManager.Application.Agents;
using InfoDumpManager.Application.Common.Events;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Events;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InfoDumpManager.Application.Agents.Orchestration;

/// <summary>
/// Coordinates multi-agent processing for GEM content.
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
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

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
                    validation = await validationAgent.ExecuteAsync(CreateContext(gemId, tenantId, contentText, options));

                    if (!validation.Success)
                    {
                        errors.AddRange(validation.Errors ?? new());
                        return CreateFailedResult(resolvedJobId, gemId, summary, summarization, categorization, tagging, validation, errors);
                    }

                    await LogValidationAsync(unitOfWork, tenantId, gemId, validation).ConfigureAwait(false);
                    _jobTracker.UpdateStatus(resolvedJobId, ProcessingStatus.Processing, 15, "Validation complete");
                }
            }

            var summarizationAgent = ResolveAgent(agentMap, AgentCapability.Summarization, errors);
            if (summarizationAgent is null)
            {
                return CreateFailedResult(resolvedJobId, gemId, summary, summarization, categorization, tagging, validation, errors);
            }

            summarization = await summarizationAgent.ExecuteAsync(CreateContext(gemId, tenantId, contentText, options));

            if (!summarization.Success)
            {
                errors.AddRange(summarization.Errors ?? new());
                return CreateFailedResult(resolvedJobId, gemId, summary, summarization, categorization, tagging, validation, errors);
            }

            summary = TryBuildSummary(summarization);
            await PersistSummaryAsync(unitOfWork, gemId, summary).ConfigureAwait(false);
            await LogSummarizationAsync(unitOfWork, tenantId, gemId, summarization).ConfigureAwait(false);
            await PublishEventAsync(mediator, new GEMSummarizationCompleted(
                gemId,
                tenantId,
                summary?.Text ?? string.Empty,
                summary?.TokenCount ?? 0,
                DateTimeOffset.UtcNow)).ConfigureAwait(false);
            _jobTracker.UpdateStatus(resolvedJobId, ProcessingStatus.Processing, 25, "Summarization complete");

            var categorizationAgent = ResolveAgent(agentMap, AgentCapability.Categorization, errors);
            if (categorizationAgent is not null)
            {
                categorization = await categorizationAgent.ExecuteAsync(CreateContext(gemId, tenantId, contentText, options));

                if (!categorization.Success)
                {
                    errors.AddRange(categorization.Errors ?? new());
                }
                else
                {
                    await HandleCategorizationAsync(unitOfWork, mediator, tenantId, gemId, categorization, options)
                        .ConfigureAwait(false);
                }
            }

            _jobTracker.UpdateStatus(resolvedJobId, ProcessingStatus.Processing, 50, "Categorization complete");

            var taggingAgent = ResolveAgent(agentMap, AgentCapability.Tagging, errors);
            if (taggingAgent is not null)
            {
                tagging = await taggingAgent.ExecuteAsync(CreateContext(gemId, tenantId, contentText, options));

                if (!tagging.Success)
                {
                    errors.AddRange(tagging.Errors ?? new());
                }
                else
                {
                    await HandleTaggingAsync(unitOfWork, mediator, tenantId, gemId, tagging).ConfigureAwait(false);
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

    private static async Task PersistSummaryAsync(IUnitOfWork unitOfWork, Guid gemId, GEMSummary? summary)
    {
        if (summary is null)
        {
            return;
        }

        if (unitOfWork.GEMs is null)
        {
            return;
        }

        InfoDumpManager.Domain.Entities.GEM? gem;
        try
        {
            gem = await unitOfWork.GEMs.GetByIdAsync(gemId);
        }
        catch
        {
            return;
        }

        if (gem is null)
        {
            return;
        }

        gem.UpdateSummary(summary);
        await unitOfWork.SaveChangesAsync();
    }

    private static async Task HandleCategorizationAsync(
        IUnitOfWork unitOfWork,
        IMediator mediator,
        Guid tenantId,
        Guid gemId,
        AgentResult categorization,
        ProcessingOptions options)
    {
        var suggestion = TryBuildCategorizationSuggestion(categorization);
        if (suggestion is null)
        {
            return;
        }

        var suggestionEntity = CategorySuggestion.Create(
            tenantId,
            gemId,
            suggestion.SuggestedCategoryId,
            suggestion.ProposedCategoryName,
            suggestion.ConfidenceScore,
            suggestion.Rationale,
            false);

        await unitOfWork.CategorySuggestions.AddAsync(suggestionEntity).ConfigureAwait(false);

        var autoAssigned = false;
        if (suggestion.SuggestedCategoryId.HasValue && suggestion.ConfidenceScore >= options.AutoApproveThreshold)
        {
            var gem = await unitOfWork.GEMs.GetByIdAsync(gemId).ConfigureAwait(false);
            var category = await unitOfWork.Categories.GetByIdAsync(suggestion.SuggestedCategoryId.Value).ConfigureAwait(false);
            if (gem is not null && category is not null && category.TenantId == tenantId)
            {
                gem.AssignCategory(category);
                autoAssigned = true;
                suggestionEntity.MarkAutoAssigned(true);

                await unitOfWork.ActivityLogs.AddAsync(ActivityLog.Create(
                    tenantId,
                    ActivityEventType.CategorizationAccepted,
                    nameof(GEM),
                    $"GEM auto-assigned to category {category.Name}",
                    gemId,
                    null,
                    BuildMetadata(new
                    {
                        gemId,
                        categoryId = category.Id,
                        categoryName = category.Name,
                        suggestion.ConfidenceScore
                    }))).ConfigureAwait(false);
            }
        }

        await unitOfWork.ActivityLogs.AddAsync(ActivityLog.Create(
            tenantId,
            ActivityEventType.CategorizationSuggested,
            nameof(GEM),
            "Categorization suggested",
            gemId,
            null,
            BuildMetadata(new
            {
                gemId,
                suggestion.SuggestedCategoryId,
                suggestion.ProposedCategoryName,
                suggestion.ConfidenceScore,
                suggestion.Rationale,
                autoAssigned
            }))).ConfigureAwait(false);

        await unitOfWork.SaveChangesAsync().ConfigureAwait(false);

        await PublishEventAsync(mediator, new GEMCategorizationSuggested(
            gemId,
            suggestion.SuggestedCategoryId,
            suggestion.ConfidenceScore,
            suggestion.ConfidenceScore < 0.6,
            DateTimeOffset.UtcNow)).ConfigureAwait(false);
    }

    private static async Task HandleTaggingAsync(
        IUnitOfWork unitOfWork,
        IMediator mediator,
        Guid tenantId,
        Guid gemId,
        AgentResult tagging)
    {
        var suggestions = TryBuildTagSuggestions(tagging);
        if (suggestions.Count == 0)
        {
            return;
        }

        await unitOfWork.ActivityLogs.AddAsync(ActivityLog.Create(
            tenantId,
            ActivityEventType.TaggingSuggested,
            nameof(GEM),
            "Tagging suggested",
            gemId,
            null,
            BuildMetadata(new
            {
                gemId,
                tags = suggestions.Select(s => new { s.TagId, s.TagName, s.SimilarityScore }).ToList()
            }))).ConfigureAwait(false);

        await unitOfWork.SaveChangesAsync().ConfigureAwait(false);

        var eventTags = suggestions
            .Select(s => new TagSuggestionDetail(s.TagId, s.TagName, s.SimilarityScore))
            .ToList();

        await PublishEventAsync(mediator, new GEMTaggingSuggested(
            gemId,
            tenantId,
            eventTags,
            DateTimeOffset.UtcNow)).ConfigureAwait(false);
    }

    private static async Task LogSummarizationAsync(
        IUnitOfWork unitOfWork,
        Guid tenantId,
        Guid gemId,
        AgentResult summarization)
    {
        var metadata = BuildMetadata(new
        {
            gemId,
            model = summarization.Data.Payload.TryGetValue("model", out var model) ? model : null,
            tokenCount = summarization.Data.Payload.TryGetValue("tokenCount", out var tokens) ? tokens : null,
            cacheHit = summarization.Data.Payload.TryGetValue("cacheHit", out var cacheHit) ? cacheHit : null,
            cost = summarization.Metrics.EstimatedCost
        });

        await unitOfWork.ActivityLogs.AddAsync(ActivityLog.Create(
            tenantId,
            ActivityEventType.SummarizationCompleted,
            nameof(GEM),
            "Summarization completed",
            gemId,
            null,
            metadata)).ConfigureAwait(false);

        await unitOfWork.SaveChangesAsync().ConfigureAwait(false);
    }

    private static async Task LogValidationAsync(
        IUnitOfWork unitOfWork,
        Guid tenantId,
        Guid gemId,
        AgentResult validation)
    {
        var metadata = BuildMetadata(new
        {
            gemId,
            status = validation.Data.Payload.TryGetValue("status", out var status) ? status : null,
            response = validation.Data.Payload.TryGetValue("response", out var response) ? response : null,
            confidence = validation.Confidence?.Score
        });

        await unitOfWork.ActivityLogs.AddAsync(ActivityLog.Create(
            tenantId,
            ActivityEventType.ValidationCompleted,
            nameof(GEM),
            "Validation completed",
            gemId,
            null,
            metadata)).ConfigureAwait(false);

        await unitOfWork.SaveChangesAsync().ConfigureAwait(false);
    }

    private static CategorizationSuggestionData? TryBuildCategorizationSuggestion(AgentResult result)
    {
        var payload = result.Data.Payload;
        Guid? categoryId = null;
        if (payload.TryGetValue("suggestedCategoryId", out var idObj)
            && idObj is string idText
            && Guid.TryParse(idText, out var parsed))
        {
            categoryId = parsed;
        }

        var name = payload.TryGetValue("suggestedCategory", out var nameObj) ? nameObj as string : null;
        var proposedName = payload.TryGetValue("proposedCategoryName", out var proposedObj) ? proposedObj as string : null;
        var confidence = payload.TryGetValue("confidence", out var confObj) && confObj is double conf
            ? conf
            : result.Confidence?.Score ?? 0.0;
        var rationale = payload.TryGetValue("rationale", out var rationaleObj) ? rationaleObj as string : null;

        if (!categoryId.HasValue && string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(proposedName))
        {
            return null;
        }

        return new CategorizationSuggestionData(categoryId, name ?? proposedName, proposedName, confidence, rationale);
    }

    private static List<TagSuggestionResult> TryBuildTagSuggestions(AgentResult result)
    {
        if (result.Data.Payload.TryGetValue("suggestedTags", out var tagsObj)
            && tagsObj is List<TagSuggestionResult> suggestions)
        {
            return suggestions;
        }

        return new List<TagSuggestionResult>();
    }

    private static JsonDocument BuildMetadata(object payload)
        => JsonDocument.Parse(JsonSerializer.Serialize(payload));

    private static Task PublishEventAsync(IMediator mediator, IDomainEvent domainEvent)
        => mediator.Publish(new DomainEventNotification(domainEvent));

    private sealed record CategorizationSuggestionData(
        Guid? SuggestedCategoryId,
        string? SuggestedCategoryName,
        string? ProposedCategoryName,
        double ConfidenceScore,
        string? Rationale);

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
