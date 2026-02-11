# Phase 3 — Orchestrator Decomposition

## Goal
Break the ~530-line `ContentProcessingOrchestrator` god class into focused components:
1. A slim orchestrator that only runs agents in sequence.
2. A `PersistenceHandler` that saves summaries, categorization suggestions, and tags.
3. An `ActivityLogger` that writes audit logs after each step.
4. Job tracking already extracted to `IJobTracker` — move status update calls to a decorator or to the background service.

## Current State

**File:** `src/InfoDumpManager.Application/Agents/Orchestration/ContentProcessingOrchestrator.cs` (~530 lines)

The orchestrator currently:
- Resolves agents from DI
- Runs validation → summarization → categorization → tagging pipeline
- After each step: persists results via `IUnitOfWork`, logs via `ActivityLog.Create()`, publishes domain events via MediatR
- Tracks job progress via `IJobTracker`
- Has inline helper methods: `PersistSummaryAsync`, `HandleCategorizationAsync`, `HandleTaggingAsync`, `LogSummarizationAsync`, `LogValidationAsync`, `TryBuildSummary`, `TryBuildCategorizationSuggestion`, `TryBuildTagSuggestions`, etc.

**Key types involved:**
- `IContentProcessingOrchestrator` (interface in `Application/Agents/Orchestration/`)
- `IJobTracker` (interface in `Application/Agents/Orchestration/`)
- `IUnitOfWork` (interface in `Domain/Repositories/`)
- `IMediator` (MediatR for domain event publishing)
- `AgentResult`, `AgentContext`, `ProcessingResult`, `ProcessingOptions` (records in `Application/Agents/`)

## Changes

### 3.1 — Create `IProcessingPersistence` interface

**New file:** `src/InfoDumpManager.Application/Agents/Orchestration/IProcessingPersistence.cs`

This interface encapsulates all persistence side-effects that happen during processing.

```csharp
using InfoDumpManager.Application.Agents.Orchestration;
using InfoDumpManager.Domain.ValueObjects;

namespace InfoDumpManager.Application.Agents.Orchestration;

/// <summary>
/// Handles persistence side-effects during agent processing.
/// </summary>
public interface IProcessingPersistence
{
    /// <summary>
    /// Persists a generated summary onto the GEM entity.
    /// </summary>
    Task PersistSummaryAsync(Guid gemId, GEMSummary? summary, CancellationToken ct = default);

    /// <summary>
    /// Creates a CategorySuggestion entity and optionally auto-assigns the category to the GEM.
    /// </summary>
    Task HandleCategorizationAsync(
        Guid tenantId,
        Guid gemId,
        AgentResult categorization,
        ProcessingOptions options,
        CancellationToken ct = default);

    /// <summary>
    /// Logs tagging suggestions for the GEM.
    /// </summary>
    Task HandleTaggingAsync(
        Guid tenantId,
        Guid gemId,
        AgentResult tagging,
        CancellationToken ct = default);
}
```

### 3.2 — Create `IProcessingActivityLogger` interface

**New file:** `src/InfoDumpManager.Application/Agents/Orchestration/IProcessingActivityLogger.cs`

```csharp
namespace InfoDumpManager.Application.Agents.Orchestration;

/// <summary>
/// Writes ActivityLog entries for processing pipeline steps.
/// </summary>
public interface IProcessingActivityLogger
{
    Task LogValidationAsync(Guid tenantId, Guid gemId, AgentResult validation, CancellationToken ct = default);
    Task LogSummarizationAsync(Guid tenantId, Guid gemId, AgentResult summarization, CancellationToken ct = default);
}
```

### 3.3 — Create `ProcessingPersistence` implementation

**New file:** `src/InfoDumpManager.Application/Agents/Orchestration/ProcessingPersistence.cs`

Move these methods from `ContentProcessingOrchestrator`:
- `PersistSummaryAsync` (lines ~290-315)
- `HandleCategorizationAsync` (lines ~317-393)
- `HandleTaggingAsync` (lines ~395-435)
- `TryBuildCategorizationSuggestion` (lines ~445-485) — keep as private helper
- `TryBuildTagSuggestions` (lines ~487-498) — keep as private helper
- `BuildMetadata` (lines ~500) — keep as private static helper

The implementation depends on `IUnitOfWork` and `IMediator`. It must be registered as **scoped** because `IUnitOfWork` is scoped.

```csharp
using System.Text.Json;
using InfoDumpManager.Application.Common.Events;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Events;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Domain.ValueObjects;
using MediatR;

namespace InfoDumpManager.Application.Agents.Orchestration;

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
        // Move the body of the existing PersistSummaryAsync from the orchestrator.
        // Guard: if summary is null or repo is null, return.
        // Fetch GEM by id, call gem.UpdateSummary(summary), save.
        if (summary is null) return;
        var gem = await _unitOfWork.GEMs.GetByIdAsync(gemId, ct).ConfigureAwait(false);
        if (gem is null) return;
        gem.UpdateSummary(summary);
        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task HandleCategorizationAsync(
        Guid tenantId, Guid gemId, AgentResult categorization,
        ProcessingOptions options, CancellationToken ct = default)
    {
        // Move the entire body of HandleCategorizationAsync from orchestrator.
        // This includes: TryBuildCategorizationSuggestion, creating CategorySuggestion entity,
        // auto-assignment logic, activity log entries, SaveChangesAsync, and publishing
        // GEMCategorizationSuggested event via _mediator.
        // (Full body copied from orchestrator lines ~317-393)
        var suggestion = TryBuildCategorizationSuggestion(categorization);
        if (suggestion is null) return;

        var suggestionEntity = CategorySuggestion.Create(
            tenantId, gemId, suggestion.SuggestedCategoryId,
            suggestion.ProposedCategoryName, suggestion.ConfidenceScore,
            suggestion.Rationale, false);

        await _unitOfWork.CategorySuggestions.AddAsync(suggestionEntity, ct).ConfigureAwait(false);

        if (suggestion.SuggestedCategoryId.HasValue
            && suggestion.ConfidenceScore >= options.AutoApproveThreshold)
        {
            var gem = await _unitOfWork.GEMs.GetByIdAsync(gemId, ct).ConfigureAwait(false);
            var category = await _unitOfWork.Categories.GetByIdAsync(
                suggestion.SuggestedCategoryId.Value, ct).ConfigureAwait(false);
            if (gem is not null && category is not null && category.TenantId == tenantId)
            {
                gem.AssignCategory(category);
                suggestionEntity.MarkAutoAssigned(true);

                await _unitOfWork.ActivityLogs.AddAsync(ActivityLog.Create(
                    tenantId, ActivityEventType.CategorizationAccepted, nameof(GEM),
                    $"GEM auto-assigned to category {category.Name}",
                    gemId, null, BuildMetadata(new
                    {
                        gemId, categoryId = category.Id, categoryName = category.Name,
                        suggestion.ConfidenceScore
                    })), ct).ConfigureAwait(false);
            }
        }

        await _unitOfWork.ActivityLogs.AddAsync(ActivityLog.Create(
            tenantId, ActivityEventType.CategorizationSuggested, nameof(GEM),
            "Categorization suggested", gemId, null,
            BuildMetadata(new
            {
                gemId, suggestion.SuggestedCategoryId, suggestion.ProposedCategoryName,
                suggestion.ConfidenceScore, suggestion.Rationale
            })), ct).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

        await _mediator.Publish(new DomainEventNotification(new GEMCategorizationSuggested(
            gemId, suggestion.SuggestedCategoryId, suggestion.ConfidenceScore,
            suggestion.ConfidenceScore < 0.6, DateTimeOffset.UtcNow)), ct).ConfigureAwait(false);
    }

    public async Task HandleTaggingAsync(
        Guid tenantId, Guid gemId, AgentResult tagging, CancellationToken ct = default)
    {
        // Move the entire body of HandleTaggingAsync from orchestrator.
        var suggestions = TryBuildTagSuggestions(tagging);
        if (suggestions.Count == 0) return;

        await _unitOfWork.ActivityLogs.AddAsync(ActivityLog.Create(
            tenantId, ActivityEventType.TaggingSuggested, nameof(GEM),
            "Tagging suggested", gemId, null,
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
            gemId, tenantId, eventTags, DateTimeOffset.UtcNow)), ct).ConfigureAwait(false);
    }

    // ---- Private helpers (moved from orchestrator) ----

    private static CategorizationSuggestionData? TryBuildCategorizationSuggestion(AgentResult result)
    {
        // Exact body from orchestrator's TryBuildCategorizationSuggestion method.
        var payload = result.Data.Payload;
        Guid? categoryId = null;
        if (payload.TryGetValue("suggestedCategoryId", out var idObj)
            && idObj is string idText && Guid.TryParse(idText, out var parsed))
        {
            categoryId = parsed;
        }

        var name = payload.TryGetValue("suggestedCategory", out var nameObj) ? nameObj as string : null;
        var proposedName = payload.TryGetValue("proposedCategoryName", out var proposedObj)
            ? proposedObj as string : null;
        var confidence = payload.TryGetValue("confidence", out var confObj) && confObj is double conf
            ? conf : result.Confidence?.Score ?? 0.0;
        var rationale = payload.TryGetValue("rationale", out var rationaleObj) ? rationaleObj as string : null;

        if (!categoryId.HasValue && string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(proposedName))
            return null;

        return new CategorizationSuggestionData(categoryId, name ?? proposedName, proposedName, confidence, rationale);
    }

    private static List<TagSuggestionResult> TryBuildTagSuggestions(AgentResult result)
    {
        if (result.Data.Payload.TryGetValue("suggestedTags", out var tagsObj)
            && tagsObj is List<TagSuggestionResult> suggestions)
            return suggestions;
        return new List<TagSuggestionResult>();
    }

    private static JsonDocument BuildMetadata(object payload)
        => JsonDocument.Parse(JsonSerializer.Serialize(payload));

    private sealed record CategorizationSuggestionData(
        Guid? SuggestedCategoryId, string? SuggestedCategoryName,
        string? ProposedCategoryName, double ConfidenceScore, string? Rationale);
}
```

### 3.4 — Create `ProcessingActivityLogger` implementation

**New file:** `src/InfoDumpManager.Application/Agents/Orchestration/ProcessingActivityLogger.cs`

Move `LogSummarizationAsync` and `LogValidationAsync` from the orchestrator.

```csharp
using System.Text.Json;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Repositories;

namespace InfoDumpManager.Application.Agents.Orchestration;

public sealed class ProcessingActivityLogger : IProcessingActivityLogger
{
    private readonly IUnitOfWork _unitOfWork;

    public ProcessingActivityLogger(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task LogValidationAsync(Guid tenantId, Guid gemId, AgentResult validation, CancellationToken ct = default)
    {
        // Body from orchestrator's LogValidationAsync
        var metadata = BuildMetadata(new
        {
            gemId,
            status = validation.Data.Payload.TryGetValue("status", out var status) ? status : null,
            response = validation.Data.Payload.TryGetValue("response", out var response) ? response : null,
            confidence = validation.Confidence?.Score
        });

        await _unitOfWork.ActivityLogs.AddAsync(ActivityLog.Create(
            tenantId, ActivityEventType.ValidationCompleted, nameof(GEM),
            "Validation completed", gemId, null, metadata), ct).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task LogSummarizationAsync(Guid tenantId, Guid gemId, AgentResult summarization, CancellationToken ct = default)
    {
        // Body from orchestrator's LogSummarizationAsync
        var metadata = BuildMetadata(new
        {
            gemId,
            model = summarization.Data.Payload.TryGetValue("model", out var model) ? model : null,
            tokenCount = summarization.Data.Payload.TryGetValue("tokenCount", out var tokens) ? tokens : null,
            cacheHit = summarization.Data.Payload.TryGetValue("cacheHit", out var cacheHit) ? cacheHit : null,
            cost = summarization.Metrics.EstimatedCost
        });

        await _unitOfWork.ActivityLogs.AddAsync(ActivityLog.Create(
            tenantId, ActivityEventType.SummarizationCompleted, nameof(GEM),
            "Summarization completed", gemId, null, metadata), ct).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static JsonDocument BuildMetadata(object payload)
        => JsonDocument.Parse(JsonSerializer.Serialize(payload));
}
```

### 3.5 — Slim down `ContentProcessingOrchestrator`

**File:** `src/InfoDumpManager.Application/Agents/Orchestration/ContentProcessingOrchestrator.cs`

**Inject** `IProcessingPersistence` and `IProcessingActivityLogger` via `IServiceScopeFactory`. Resolve them per-scope alongside other scoped services.

**Remove** all private methods that were moved:
- `PersistSummaryAsync`
- `HandleCategorizationAsync`
- `HandleTaggingAsync`
- `LogSummarizationAsync`
- `LogValidationAsync`
- `TryBuildCategorizationSuggestion`
- `TryBuildTagSuggestions`
- `BuildMetadata`
- `PublishEventAsync`
- The `CategorizationSuggestionData` nested record

**Replace** their call sites in `ProcessGEMAsync` with calls to the injected services.

The orchestrator constructor signature becomes:
```csharp
public ContentProcessingOrchestrator(
    IServiceScopeFactory scopeFactory,
    IJobTracker jobTracker,
    ILogger<ContentProcessingOrchestrator> logger)
```
(Unchanged — the two new interfaces are resolved per-scope inside `ProcessGEMAsync`.)

Inside `ProcessGEMAsync`, after creating the scope:
```csharp
await using var scope = _scopeFactory.CreateAsyncScope();
var agents = scope.ServiceProvider.GetServices<IAgent>().ToList();
var agentMap = agents.GroupBy(a => a.Capability).ToDictionary(g => g.Key, g => g.First());
var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
var persistence = scope.ServiceProvider.GetRequiredService<IProcessingPersistence>();
var activityLogger = scope.ServiceProvider.GetRequiredService<IProcessingActivityLogger>();
```

Then replace inline calls:
| Old call | New call |
|---|---|
| `await LogValidationAsync(unitOfWork, tenantId, gemId, validation)` | `await activityLogger.LogValidationAsync(tenantId, gemId, validation)` |
| `await PersistSummaryAsync(unitOfWork, gemId, summary)` | `await persistence.PersistSummaryAsync(gemId, summary)` |
| `await LogSummarizationAsync(unitOfWork, tenantId, gemId, summarization)` | `await activityLogger.LogSummarizationAsync(tenantId, gemId, summarization)` |
| `await PublishEventAsync(mediator, new GEMSummarizationCompleted(...))` | `await mediator.Publish(new DomainEventNotification(new GEMSummarizationCompleted(...)))` |
| `await HandleCategorizationAsync(unitOfWork, mediator, tenantId, gemId, categorization, options)` | `await persistence.HandleCategorizationAsync(tenantId, gemId, categorization, options)` |
| `await HandleTaggingAsync(unitOfWork, mediator, tenantId, gemId, tagging)` | `await persistence.HandleTaggingAsync(tenantId, gemId, tagging)` |

The `unitOfWork` and `mediator` local variables are no longer needed directly in the orchestrator (only used by persistence/logger). Keep `mediator` only if `GEMSummarizationCompleted` domain event publishing stays in the orchestrator (it arguably should be in `PersistSummaryAsync` instead — move it there).

The `TryBuildSummary` method stays in the orchestrator because it builds a value object from `AgentResult` — it's orchestration logic, not persistence.

### 3.6 — Register new services in DI

**File:** `src/InfoDumpManager.Infrastructure/DependencyInjection.cs`

Add after the existing orchestrator registration:
```csharp
services.AddScoped<IProcessingPersistence, ProcessingPersistence>();
services.AddScoped<IProcessingActivityLogger, ProcessingActivityLogger>();
```

These must be **scoped** because they depend on `IUnitOfWork` (which is scoped).

### 3.7 — Final orchestrator structure

After all changes, the orchestrator should be approximately **150-180 lines** and contain:
- Constructor (3 deps: `IServiceScopeFactory`, `IJobTracker`, `ILogger`)
- `ProcessGEMAsync` — the main pipeline: resolve scope → validate → summarize → categorize → tag → return result
- `ProcessBatchAsync` — fan-out with `SemaphoreSlim`
- `GetJobStatusAsync` → delegates to `_jobTracker`
- `WatchJobAsync` → delegates to `_jobTracker`
- `CreateContext` — builds `AgentContext` from parameters
- `ResolveAgent` — capability lookup
- `EstimateTokens` — simple heuristic
- `TryBuildSummary` — builds `GEMSummary` from `AgentResult`
- `CreateFailedResult` — builds failure `ProcessingResult`

## Verification

```bash
dotnet build
dotnet test
```

Verify the orchestrator file line count is significantly reduced:
```bash
wc -l src/InfoDumpManager.Application/Agents/Orchestration/ContentProcessingOrchestrator.cs
# Should be ~150-180 lines, down from ~530
```
