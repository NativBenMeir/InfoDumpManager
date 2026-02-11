# Phase 4 — Agent Repository Removal (SummarizationAgent)

## Goal
The `SummarizationAgent` (and `CategorizationAgent`) break the agent abstraction by directly injecting `IGEMRepository` and fetching GEM data. Agents should operate only on the `AgentContext` they receive. The orchestrator already passes `ContentText`, `GEMId`, and `TenantId` — everything the agent needs.

## Current State

### SummarizationAgent
**File:** `src/InfoDumpManager.Application/Agents/Implementations/SummarizationAgent.cs`

Problematic constructor dependency and usage:
```csharp
private readonly IGEMRepository _gemRepository;

public SummarizationAgent(
    ILLMProvider llmProvider,
    ILLMRateLimiter rateLimiter,
    IGEMRepository gemRepository,   // ← should not be here
    ITextCache textCache,
    ICostManager costManager,
    ILogger<SummarizationAgent> logger)
```

In `ExecuteAsync`, the agent re-fetches the GEM:
```csharp
var gem = await _gemRepository.GetByIdAsync(context.GEMId).ConfigureAwait(false);
if (gem is null || gem.TenantId != context.TenantId)
{
    return CreateFailureResult(context, "GEM not found for tenant.", ...);
}
// ...
var content = string.IsNullOrWhiteSpace(context.ContentText)
    ? BuildContentFromGem(gem)
    : context.ContentText;
```

The `BuildContentFromGem` method:
```csharp
private static string BuildContentFromGem(GEM gem)
    => $"Title: {gem.Title}\n\n{gem.Snapshot.HtmlContent}";
```

### CategorizationAgent
**File:** `src/InfoDumpManager.Application/Agents/Implementations/CategorizationAgent.cs`

Similarly injects `IGEMRepository` and `ICategoryRepository`, re-fetches GEM and categories.

The `ICategoryRepository` dependency is harder to remove because the agent needs the list of existing categories for the LLM prompt. We handle this by enriching the `AgentContext` (see 4.3).

## Changes

### 4.1 — Remove `IGEMRepository` from `SummarizationAgent`

**File:** `src/InfoDumpManager.Application/Agents/Implementations/SummarizationAgent.cs`

1. Remove the `IGEMRepository _gemRepository` field.
2. Remove `IGEMRepository gemRepository` from the constructor params and the assignment.
3. In `ExecuteAsync`, remove the `_gemRepository.GetByIdAsync` call and the tenant-mismatch guard.
4. Use `context.ContentText` directly. If it's empty, return a failure result ("No content provided").
5. Remove `BuildContentFromGem` method.

**Updated constructor:**
```csharp
public SummarizationAgent(
    ILLMProvider llmProvider,
    ILLMRateLimiter rateLimiter,
    ITextCache textCache,
    ICostManager costManager,
    ILogger<SummarizationAgent> logger)
{
    _llmProvider = llmProvider;
    _rateLimiter = rateLimiter;
    _textCache = textCache;
    _costManager = costManager;
    _logger = logger;
}
```

**Updated `ExecuteAsync` start:**
```csharp
public async Task<AgentResult> ExecuteAsync(AgentContext context)
{
    if (string.IsNullOrWhiteSpace(context.ContentText))
    {
        return CreateFailureResult(context, "No content provided for summarization.",
            TimeSpan.Zero, 0, 0m, "no-content");
    }

    var options = new SummarizationOptions();
    var content = context.ContentText;

    var cacheKey = BuildCacheKey(context.TenantId, content);
    // ... rest of the method unchanged
```

### 4.2 — Enrich content in the orchestrator before calling agents

The orchestrator (`ContentProcessingOrchestrator`) already has access to the GEM via `IUnitOfWork`. Before calling agents, it should ensure `contentText` includes the GEM title if needed.

**File:** `src/InfoDumpManager.Application/Agents/Orchestration/ContentProcessingOrchestrator.cs`

In `ProcessGEMAsync`, after creating the scope, enrich the content:

```csharp
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
```

Then pass `enrichedContent` to `CreateContext` instead of `contentText`.

### 4.3 — Handle `CategorizationAgent` repository dependencies

The CategorizationAgent needs categories for its LLM prompt. Two approaches:

**Option A (recommended): Add categories to `AgentContextMetadata.CustomData`**

In the orchestrator, before calling the categorization agent, load the categories and put them in the context:

```csharp
// In ProcessGEMAsync, before the categorization agent call:
var categories = await unitOfWork.Categories.ListByTenantAsync(tenantId).ConfigureAwait(false);
var categorizationContext = CreateContext(gemId, tenantId, enrichedContent, options);
// Add categories to custom data
categorizationContext.Metadata.CustomData["categories"] = categories;
categorization = await categorizationAgent.ExecuteAsync(categorizationContext);
```

Then in `CategorizationAgent.ExecuteAsync`:
```csharp
// Instead of:  var categories = await _categoryRepository.ListByTenantAsync(...)
IReadOnlyCollection<Category> categories;
if (context.Metadata.CustomData.TryGetValue("categories", out var catObj)
    && catObj is IReadOnlyCollection<Category> loaded)
{
    categories = loaded;
}
else
{
    categories = Array.Empty<Category>();
}
```

Remove `IGEMRepository` and `ICategoryRepository` from `CategorizationAgent`'s constructor.

**Updated CategorizationAgent constructor:**
```csharp
public CategorizationAgent(
    ILLMProvider llmProvider,
    ILLMRateLimiter rateLimiter,
    ICostManager costManager,
    ILogger<CategorizationAgent> logger)
{
    _llmProvider = llmProvider;
    _rateLimiter = rateLimiter;
    _costManager = costManager;
    _logger = logger;
}
```

**Remove from `ExecuteAsync`:**
- The `_gemRepository.GetByIdAsync` call and tenant guard
- The `_categoryRepository.ListByTenantAsync` call
- The `gem.Summary.Text` usage (use `context.ContentText` instead, which is already enriched)

**Replace content extraction:**
```csharp
// Old:
var content = string.IsNullOrWhiteSpace(gem.Summary.Text)
    ? context.ContentText
    : $"{gem.Title}\n{gem.Summary.Text}";

// New:
var content = context.ContentText;
```

### 4.4 — Update DI registrations

No DI changes needed — the agents are resolved with `GetServices<IAgent>()` and their constructors will have fewer parameters. The removed repository parameters simply won't be injected.

## Verification

```bash
dotnet build
dotnet test
```

Verify no agent imports repository interfaces:
```bash
grep -n "IGEMRepository\|ICategoryRepository" src/InfoDumpManager.Application/Agents/Implementations/*.cs
# Should have zero matches
```
