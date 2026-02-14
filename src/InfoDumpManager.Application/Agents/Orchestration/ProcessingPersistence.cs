using System.Text.Json;
using InfoDumpManager.Application.Common.Events;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Events;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Domain.ValueObjects;
using MediatR;

namespace InfoDumpManager.Application.Agents.Orchestration;

/// <summary>
/// Handles persistence side-effects during agent processing.
/// </summary>
public sealed class ProcessingPersistence : IProcessingPersistence
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;

    public ProcessingPersistence(IUnitOfWork unitOfWork, IMediator mediator)
    {
        _unitOfWork = unitOfWork;
        _mediator = mediator;
    }

    public async Task PersistSummaryAsync(Guid gemId, GEMSummary? summary, CancellationToken ct = default)
    {
        if (summary is null)
        {
            return;
        }

        if (_unitOfWork.GEMs is null)
        {
            return;
        }

        Domain.Entities.GEM? gem;
        try
        {
            gem = await _unitOfWork.GEMs.GetByIdAsync(gemId, ct).ConfigureAwait(false);
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
        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task HandleCategorizationAsync(
        Guid tenantId,
        Guid gemId,
        AgentResult categorization,
        ProcessingOptions options,
        CancellationToken ct = default)
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

        await _unitOfWork.CategorySuggestions.AddAsync(suggestionEntity, ct).ConfigureAwait(false);

        var autoAssigned = false;
        if (suggestion.SuggestedCategoryId.HasValue && suggestion.ConfidenceScore >= options.AutoApproveThreshold)
        {
            var gem = await _unitOfWork.GEMs.GetByIdAsync(gemId, ct).ConfigureAwait(false);
            var category = await _unitOfWork.Categories.GetByIdAsync(
                suggestion.SuggestedCategoryId.Value, ct).ConfigureAwait(false);
            if (gem is not null && category is not null && category.TenantId == tenantId)
            {
                gem.AssignCategory(category);
                autoAssigned = true;
                suggestionEntity.MarkAutoAssigned(true);

                await _unitOfWork.ActivityLogs.AddAsync(ActivityLog.Create(
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
                    })), ct).ConfigureAwait(false);
            }
        }

        await _unitOfWork.ActivityLogs.AddAsync(ActivityLog.Create(
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
            })), ct).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

        await _mediator.Publish(new DomainEventNotification(new GEMCategorizationSuggested(
            gemId,
            suggestion.SuggestedCategoryId,
            suggestion.ConfidenceScore,
            suggestion.ConfidenceScore < 0.6,
            DateTimeOffset.UtcNow)), ct).ConfigureAwait(false);
    }

    public async Task HandleTaggingAsync(
        Guid tenantId,
        Guid gemId,
        AgentResult tagging,
        CancellationToken ct = default)
    {
        var suggestions = TryBuildTagSuggestions(tagging);
        if (suggestions.Count == 0)
        {
            return;
        }

        await _unitOfWork.ActivityLogs.AddAsync(ActivityLog.Create(
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
            })), ct).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

        var eventTags = suggestions
            .Select(s => new TagSuggestionDetail(s.TagId, s.TagName, s.SimilarityScore))
            .ToList();

        await _mediator.Publish(new DomainEventNotification(new GEMTaggingSuggested(
            gemId,
            tenantId,
            eventTags,
            DateTimeOffset.UtcNow)), ct).ConfigureAwait(false);
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

    private sealed record CategorizationSuggestionData(
        Guid? SuggestedCategoryId,
        string? SuggestedCategoryName,
        string? ProposedCategoryName,
        double ConfidenceScore,
        string? Rationale);
}
