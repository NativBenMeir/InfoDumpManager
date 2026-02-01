# AI Agents Architecture Design
**For Epic: AI Agents for Intelligent Content Processing**

**Version:** 1.0  
**Date:** February 1, 2026  
**Design Pattern:** Multi-Agent System with Coordinator Pattern

---

## 1. Executive Summary

This document defines an **AI Agent-based architecture** for implementing intelligent content processing in InfoDumpManager. The design employs a **coordinator-agent pattern** with a **hybrid framework implementation**:

- **Semantic Kernel**: Prompt templates, LLM execution, semantic memory
- **Polly**: Resilience (retries, circuit breakers, timeouts)
- **MediatR**: Orchestration and domain event publishing
- **Kernel Memory (optional)**: RAG + pgvector integration

Specialized agents handle distinct responsibilities:

- **Orchestrator Agent**: Manages workflow, routes work, handles state transitions (MediatR)
- **Summarization Agent**: Generates concise summaries via SK + LLM
- **Categorization Agent**: Suggests categories using embeddings + LLM
- **Tagging Agent**: Generates semantic tags and embeddings
- **Validation Agent**: Quality checks and fallback logic
- **Cost Manager Agent**: Tracks usage and enforces budgets

This multi-agent approach provides:
- **Separation of Concerns**: Interfaces in Application, adapters in Infrastructure
- **Extensibility**: New agents/providers without changing orchestration
- **Resilience**: Polly-backed provider calls
- **Observability**: Structured logs and metrics per agent
- **Testability**: Agents mocked via interfaces; adapters isolated

---

## 2. Core Architecture

### 2.1 High-Level Component Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                      PRESENTATION LAYER                         │
│        (WebAPI Controllers, Web UI, Browser Extension)          │
└──────────────────────────┬──────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────────┐
│                  APPLICATION ORCHESTRATION                      │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  Orchestrator (Coordinator Pattern + MediatR)           │  │
│  │  • Routes incoming GEMs to processing queue             │  │
│  │  • Coordinates multi-step AI workflows                  │  │
│  │  • Manages state machine (Pending→Processing→Complete)  │  │
│  │  • Emits domain events via MediatR                      │  │
│  └──────────────────────────────────────────────────────────┘  │
└──────────────────┬──────────────────────────┬──────────────────┘
                   │                          │
        ┌──────────▼──────────────┐  ┌───────▼──────────────┐
        │ In-Memory Job Queue     │  │ Domain Events Bus    │
    │ (IJobQueue<T>)          │  │ (MediatR Publisher)  │
        │ • Pending jobs          │  │ • Real-time updates  │
        │ • Retry logic           │  │ • Activity logging   │
        │ • Concurrency limits    │  │ • UI notifications   │
        └──────────┬──────────────┘  └─────────────────────┘
                   │
        ┌──────────▼────────────────────────────────────────┐
        │         AGENT PROCESSING LAYER                    │
        │  (IAgent interface + Specialized Implementations) │
        │  • SK-backed LLM calls                             │
        │  • Polly resilience policies                       │
        └──────────────────┬───────────────────────────────┘
                   │
        ┌──────────────────▼────────────────────────────┐
        │     EXTERNAL AI PROVIDERS & SERVICES          │
        │                                               │
        │  ┌────────────────────────────────────────┐  │
        │  │ Semantic Kernel                         │  │
        │  │ • Prompt templates                      │  │
        │  │ • LLM execution                         │  │
        │  │ • Semantic memory                       │  │
        │  └────────────────────────────────────────┘  │
        │                                               │
        │  ┌────────────────────────────────────────┐  │
        │  │ Polly Resilience                         │ │
        │  │ • Retry + circuit breaker + timeout      │ │
        │  └────────────────────────────────────────┘  │
        │                                               │
        │  ┌────────────────────────────────────────┐  │
        │  │ Kernel Memory (optional)                │  │
        │  │ • RAG + pgvector integration            │  │
        │  └────────────────────────────────────────┘  │
        │                                               │
        └───────────────────────────────────────────────┘
                           │
        ┌──────────────────▼──────────────────────┐
        │     PERSISTENCE & CACHING LAYER        │
        │                                        │
        │  • PostgreSQL (GEM, Category, etc.)    │
        │  • pgvector (embedding storage)        │
        │  • Redis (embedding cache + sessions)  │
        │  • Activity Log (audit trail)          │
        └────────────────────────────────────────┘
```

### 2.2 Agent Interface Design

```csharp
namespace InfoDumpManager.Application.Agents;

/// <summary>
/// Base interface for all AI agents. Each agent encapsulates
/// a specific AI operation with error handling and observability.
/// </summary>
public interface IAgent
{
    string Name { get; }
    AgentCapability Capability { get; }
    
    /// <summary>
    /// Executes the agent's operation. Returns result with metadata.
    /// </summary>
    Task<AgentResult> ExecuteAsync(AgentContext context);
}

/// <summary>
/// Defines types of operations agents can perform.
/// </summary>
public enum AgentCapability
{
    Summarization,
    Categorization,
    Tagging,
    Validation,
    CostManagement,
    Orchestration
}

/// <summary>
/// Input context for agent execution.
/// </summary>
public record AgentContext(
    Guid GEMId,
    Guid TenantId,
    string ContentText,
    AgentContextMetadata Metadata);

public record AgentContextMetadata(
    string ContentSource,
    int EstimatedTokenCount,
    DateTimeOffset CreatedAt,
    Dictionary<string, object> CustomData);

/// <summary>
/// Output from agent execution. All agents return standardized results.
/// </summary>
public record AgentResult(
    bool Success,
    string Message,
    AgentResultData Data,
    AgentMetrics Metrics,
    List<string>? Errors = null,
    AgentResultConfidence? Confidence = null);

public record AgentResultData(
    string AgentName,
    DateTimeOffset ExecutedAt,
    Dictionary<string, object> Payload);

public record AgentMetrics(
    int TokensUsed,
    decimal EstimatedCost,
    TimeSpan ExecutionTime,
    int RetryCount,
    string ProviderUsed);

public record AgentResultConfidence(
    double Score, // 0.0 to 1.0
    bool RequiresManualReview,
    string Reasoning);
```

### 2.3 Specialized Agent Implementations

#### **2.3.1 Summarization Agent**

```csharp
namespace InfoDumpManager.Application.Agents.Implementations;

public interface ISummarizationAgent : IAgent
{
    Task<SummarizationResult> SummarizeAsync(
        string content, 
        SummarizationOptions options);
}

public record SummarizationOptions(
    SummaryLength Length = SummaryLength.Medium,
    string Language = "en",
    bool PreserveCodeBlocks = true);

public enum SummaryLength
{
    Short,    // 1-2 sentences
    Medium,   // 3-5 sentences
    Detailed  // Full paragraph
}

public record SummarizationResult(
    string Summary,
    int InputTokens,
    int OutputTokens,
    string ModelUsed,
    DateTimeOffset GeneratedAt);

/// <summary>
/// Implementation with hybrid stack:
/// - Semantic Kernel for prompt templates + LLM calls
/// - Polly resilience at provider layer
/// - Cost Manager for token budgeting
/// - Fallback: Extractive summarization
/// </summary>
public class SummarizationAgent : ISummarizationAgent
{
    private readonly ILLMProvider _llmProvider; // SK-backed adapter
    private readonly IPromptTemplate _promptTemplate;
    private readonly ITokenCounter _tokenCounter;
    private readonly ICostManager _costManager;
    private readonly ILogger<SummarizationAgent> _logger;

    public string Name => "SummarizationAgent";
    public AgentCapability Capability => AgentCapability.Summarization;

    public async Task<AgentResult> ExecuteAsync(AgentContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var errors = new List<string>();
        int totalTokens = 0;

        try
        {
            // Build prompt with template + content
            var prompt = _promptTemplate.BuildSummarizationPrompt(
                context.ContentText,
                context.Metadata.CustomData.GetValueOrDefault("language", "en") as string);

            // Estimate tokens before calling API
            var estimatedTokens = _tokenCounter.CountTokens(prompt);
            
            // Check budget
            var budgetCheck = await _costManager.CanProcessAsync(
                context.TenantId, 
                estimatedTokens);
            
            if (!budgetCheck.Allowed)
            {
                return AgentResult.Failure(
                    "Budget exceeded",
                    new { reason = budgetCheck.Reason },
                    errors: new() { budgetCheck.Message });
            }

            // Execute LLM call with retry policy
            LLMResponse response = null;
            try
            {
                response = await _llmProvider.CallAsync(
                    prompt,
                    model: "gpt-4",
                    maxTokens: 300,
                    temperature: 0.3f); // Lower temperature for consistency
                    
                totalTokens = response.TokensUsed;
            }
            catch (LLMProviderException ex) when (ex.IsRetryable)
            {
                _logger.LogWarning($"LLM call failed, attempting fallback. Error: {ex.Message}");
                errors.Add($"Primary LLM failed: {ex.Message}");
                
                // Fallback to cheaper model
                response = await _llmProvider.CallAsync(
                    prompt,
                    model: "gpt-3.5-turbo",
                    maxTokens: 300);
                    
                totalTokens = response.TokensUsed;
            }

            // Record cost
            await _costManager.RecordUsageAsync(
                context.TenantId,
                context.GEMId,
                "summarization",
                totalTokens,
                response.CostEstimate);

            stopwatch.Stop();

            return new AgentResult(
                Success: true,
                Message: "Summarization completed",
                Data: new(
                    AgentName: Name,
                    ExecutedAt: DateTimeOffset.UtcNow,
                    Payload: new Dictionary<string, object>
                    {
                        { "summary", response.Content },
                        { "model", response.Model },
                        { "finish_reason", response.FinishReason }
                    }),
                Metrics: new(
                    TokensUsed: totalTokens,
                    EstimatedCost: response.CostEstimate,
                    ExecutionTime: stopwatch.Elapsed,
                    RetryCount: errors.Count,
                    ProviderUsed: response.Provider),
                Errors: errors.Count > 0 ? errors : null,
                Confidence: new(
                    Score: 0.95, // High confidence for LLM output
                    RequiresManualReview: false,
                    Reasoning: "LLM-generated summary"));
        }
        catch (Exception ex)
        {
            _logger.LogError($"Summarization agent failed: {ex.Message}");
            
            // Fallback to extractive summarization
            var extractiveSummary = ExtractSummaryFallback(context.ContentText);
            
            return new AgentResult(
                Success: true,
                Message: "Summarization completed (fallback mode)",
                Data: new(
                    AgentName: Name,
                    ExecutedAt: DateTimeOffset.UtcNow,
                    Payload: new Dictionary<string, object>
                    {
                        { "summary", extractiveSummary },
                        { "mode", "extractive" }
                    }),
                Metrics: new(
                    TokensUsed: 0,
                    EstimatedCost: 0m,
                    ExecutionTime: stopwatch.Elapsed,
                    RetryCount: 0,
                    ProviderUsed: "fallback"),
                Errors: new() { $"LLM failed, using fallback: {ex.Message}" },
                Confidence: new(
                    Score: 0.6, // Lower confidence for extractive
                    RequiresManualReview: true,
                    Reasoning: "Extractive fallback used due to API error"));
        }
    }

    private string ExtractSummaryFallback(string content)
    {
        // Simple extractive summarization: get first N sentences
        var sentences = content.Split('.', '!', '?')
            .Take(5)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
            
        return string.Join(". ", sentences) + ".";
    }
}
```

#### **2.3.2 Categorization Agent**

```csharp
namespace InfoDumpManager.Application.Agents.Implementations;

public interface ICategorizationAgent : IAgent
{
    Task<CategorizationResult> CategorizeAsync(
        string summary,
        IEnumerable<CategoryOption> existingCategories);
}

public record CategoryOption(
    Guid Id,
    string Name,
    string? Description,
    int GEMCount);

public record CategorizationResult(
    Guid? SuggestedCategoryId,
    string? SuggestedCategoryName,
    double ConfidenceScore,
    List<(Guid CategoryId, double SimilarityScore)> AlternativeMatches,
    bool ShouldCreateNewCategory);

/// <summary>
/// Categorization strategy:
/// 1. Semantic matching: Compare summary embedding to category embeddings
/// 2. LLM-based: Ask LLM to suggest category based on content
/// 3. Fallback: Return top-3 by similarity; flag for manual review if all < threshold
/// Hybrid stack:
/// - Embeddings via Kernel Memory or provider adapter
/// - LLM suggestion via SK-backed provider
/// - Polly retries at provider layer
/// </summary>
public class CategorizationAgent : ICategorizationAgent
{
    private readonly IEmbeddingProvider _embeddingProvider; // KM optional
    private readonly ILLMProvider _llmProvider;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ICostManager _costManager;
    private readonly ILogger<CategorizationAgent> _logger;

    public string Name => "CategorizationAgent";
    public AgentCapability Capability => AgentCapability.Categorization;

    public async Task<AgentResult> ExecuteAsync(AgentContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var errors = new List<string>();

        try
        {
            // Get existing categories
            var categories = await _categoryRepository.GetByCategoryTenantAsync(context.TenantId);
            
            if (categories.Count == 0)
            {
                // No categories exist yet - suggest creating first category
                return AgentResult.Success(
                    "No categories available",
                    new { should_create_new = true },
                    new(
                        TokensUsed: 0,
                        EstimatedCost: 0m,
                        ExecutionTime: stopwatch.Elapsed,
                        RetryCount: 0,
                        ProviderUsed: "none"),
                    confidence: new(0.5, true, "No existing categories"));
            }

            // Strategy 1: Semantic embedding similarity
            var summaryEmbedding = await _embeddingProvider.GetEmbeddingAsync(
                context.ContentText);
                
            var similarityScores = new Dictionary<Guid, double>();
            
            foreach (var category in categories)
            {
                var categoryEmbedding = await _embeddingProvider.GetEmbeddingAsync(
                    $"{category.Name} {category.Description}",
                    useCacheIfAvailable: true);
                    
                var similarity = CosineSimilarity(summaryEmbedding, categoryEmbedding);
                similarityScores[category.Id] = similarity;
            }

            // Sort by similarity
            var sortedScores = similarityScores
                .OrderByDescending(x => x.Value)
                .ToList();

            var topMatch = sortedScores.First();
            var topCategory = categories.First(c => c.Id == topMatch.Key);

            // Determine if we should suggest new category
            var confidenceScore = topMatch.Value;
            var shouldCreateNew = confidenceScore < 0.5;

            // If confidence too low, ask LLM for suggestion
            if (confidenceScore < 0.7)
            {
                var llmSuggestion = await _llmProvider.CallAsync(
                    $"Suggest the best category from this list for: {context.ContentText}\n\n" +
                    $"Categories: {string.Join(", ", categories.Select(c => c.Name))}",
                    model: "gpt-3.5-turbo",
                    maxTokens: 50);
                    
                // Parse LLM response and find matching category
                var llmCategoryName = ExtractCategoryName(llmSuggestion.Content);
                var llmCategory = categories.FirstOrDefault(c => 
                    c.Name.Equals(llmCategoryName, StringComparison.OrdinalIgnoreCase));
                
                if (llmCategory != null)
                {
                    topMatch = new(llmCategory.Id, 0.85); // Give LLM suggestion higher weight
                    topCategory = llmCategory;
                    confidenceScore = 0.85;
                }
            }

            stopwatch.Stop();

            return new AgentResult(
                Success: true,
                Message: "Categorization completed",
                Data: new(
                    AgentName: Name,
                    ExecutedAt: DateTimeOffset.UtcNow,
                    Payload: new Dictionary<string, object>
                    {
                        { "suggested_category_id", topMatch.Key },
                        { "suggested_category_name", topCategory.Name },
                        { "confidence_score", confidenceScore },
                        { "alternative_matches", sortedScores
                            .Skip(1)
                            .Take(3)
                            .ToDictionary(x => x.Key.ToString(), x => x.Value) },
                        { "should_create_new_category", shouldCreateNew }
                    }),
                Metrics: new(
                    TokensUsed: 100, // Embedding tokens
                    EstimatedCost: 0.001m,
                    ExecutionTime: stopwatch.Elapsed,
                    RetryCount: 0,
                    ProviderUsed: "embeddings"),
                Confidence: new(
                    Score: confidenceScore,
                    RequiresManualReview: confidenceScore < 0.7,
                    Reasoning: $"Semantic similarity: {confidenceScore:P1}"));
        }
        catch (Exception ex)
        {
            _logger.LogError($"Categorization agent failed: {ex.Message}");
            
            return AgentResult.Failure(
                "Categorization failed",
                new { },
                new(0, 0m, stopwatch.Elapsed, 0, "none"),
                errors: new() { ex.Message });
        }
    }

    private double CosineSimilarity(float[] vec1, float[] vec2)
    {
        var dotProduct = vec1.Zip(vec2).Sum(p => p.First * p.Second);
        var magnitude1 = Math.Sqrt(vec1.Sum(x => x * x));
        var magnitude2 = Math.Sqrt(vec2.Sum(x => x * x));
        
        return dotProduct / (magnitude1 * magnitude2);
    }

    private string ExtractCategoryName(string llmResponse)
    {
        // Parse LLM response to extract category name
        // Simple implementation: take first line or look for quoted text
        return llmResponse.Split('\n').First().Trim('"', '\'');
    }
}
```

#### **2.3.3 Tagging Agent**

```csharp
namespace InfoDumpManager.Application.Agents.Implementations;

public interface ITaggingAgent : IAgent
{
    Task<TaggingResult> GenerateTagsAsync(string summary);
}

public record TaggingResult(
    List<Tag> GeneratedTags,
    float[] Embedding,
    int TokensUsed);

public record Tag(
    string Label,
    double Relevance, // 0.0 to 1.0
    string? Description);

/// <summary>
/// Tagging strategy:
/// 1. LLM-based: Ask GPT to extract key concepts
/// 2. NER: Named entity recognition for proper nouns
/// 3. TF-IDF: Fallback statistical approach
/// 4. Embedding: Generate vector for semantic search
/// Hybrid stack:
/// - Tag extraction via SK-backed LLM provider
/// - Embeddings via Kernel Memory or provider adapter
/// - Polly retries at provider layer
/// </summary>
public class TaggingAgent : ITaggingAgent
{
    private readonly ILLMProvider _llmProvider; // SK-backed adapter
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly INLPService _nlpService;
    private readonly ICostManager _costManager;
    private readonly ILogger<TaggingAgent> _logger;

    public string Name => "TaggingAgent";
    public AgentCapability Capability => AgentCapability.Tagging;

    public async Task<AgentResult> ExecuteAsync(AgentContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // LLM-based tag extraction
            var prompt = $@"Extract 3-10 key concepts/tags from this text.
Return as JSON array: [{{tag: string, relevance: 0.0-1.0, description: string}}]

Text: {context.ContentText}";

            var llmResponse = await _llmProvider.CallAsync(
                prompt,
                model: "gpt-3.5-turbo",
                maxTokens: 200);

            var tags = ParseTagsFromLLM(llmResponse.Content);

            // Generate embedding for semantic search
            var embedding = await _embeddingProvider.GetEmbeddingAsync(context.ContentText);

            // Optionally: Run NER to extract entities
            var entities = await _nlpService.ExtractEntitiesAsync(context.ContentText);
            var entityTags = entities
                .Select(e => new Tag(
                    Label: e.Text,
                    Relevance: 0.8,
                    Description: e.Type))
                .ToList();

            // Merge LLM tags + entity tags, deduplicate
            var allTags = MergeAndDeduplicate(tags, entityTags);

            stopwatch.Stop();

            return new AgentResult(
                Success: true,
                Message: "Tags generated",
                Data: new(
                    AgentName: Name,
                    ExecutedAt: DateTimeOffset.UtcNow,
                    Payload: new Dictionary<string, object>
                    {
                        { "tags", allTags },
                        { "embedding_dimensions", embedding.Length }
                    }),
                Metrics: new(
                    TokensUsed: llmResponse.TokensUsed,
                    EstimatedCost: llmResponse.CostEstimate,
                    ExecutionTime: stopwatch.Elapsed,
                    RetryCount: 0,
                    ProviderUsed: "gpt"));
        }
        catch (Exception ex)
        {
            _logger.LogError($"Tagging agent failed: {ex.Message}");
            
            // Fallback: TF-IDF based tagging
            var tfIdfTags = _nlpService.ExtractTagsViaTFIDF(context.ContentText, topK: 7);
            
            return new AgentResult(
                Success: true,
                Message: "Tags generated (fallback)",
                Data: new(
                    AgentName: Name,
                    ExecutedAt: DateTimeOffset.UtcNow,
                    Payload: new Dictionary<string, object>
                    {
                        { "tags", tfIdfTags },
                        { "mode", "tfidf_fallback" }
                    }),
                Metrics: new(0, 0m, stopwatch.Elapsed, 0, "tfidf"));
        }
    }

    private List<Tag> ParseTagsFromLLM(string response)
    {
        try
        {
            var jsonArray = JsonSerializer.Deserialize<List<JsonElement>>(response);
            return jsonArray?.Select(elem => new Tag(
                Label: elem.GetProperty("tag").GetString() ?? "",
                Relevance: elem.GetProperty("relevance").GetDouble(),
                Description: elem.GetProperty("description").GetString()))
                .ToList() ?? new();
        }
        catch
        {
            return new();
        }
    }

    private List<Tag> MergeAndDeduplicate(List<Tag> llmTags, List<Tag> entityTags)
    {
        var combined = llmTags.Concat(entityTags)
            .GroupBy(t => t.Label.ToLower())
            .Select(g => g.OrderByDescending(x => x.Relevance).First())
            .OrderByDescending(t => t.Relevance)
            .Take(10)
            .ToList();
            
        return combined;
    }
}
```

#### **2.3.4 Validation Agent**

```csharp
namespace InfoDumpManager.Application.Agents.Implementations;

public interface IValidationAgent : IAgent
{
    Task<ValidationResult> ValidateAsync(
        Guid gemId,
        SummarizationResult summary,
        CategorizationResult categorization,
        TaggingResult tagging);
}

public record ValidationResult(
    bool IsValid,
    List<ValidationIssue> Issues,
    List<string> Warnings,
    ValidationStatus Status);

public enum ValidationStatus
{
    Approved,        // All checks passed
    ApprovedWithWarnings, // Passed but has warnings
    NeedsReview,     // Requires manual review
    Rejected         // Failed critical checks
}

public record ValidationIssue(
    string Category,
    string Message,
    bool IsCritical,
    string? Suggestion);

/// <summary>
/// Validation checks:
/// 1. Summary length: 50-500 characters
/// 2. Categorization confidence: > 0.5 (warn if < 0.7)
/// 3. Tag quality: at least 3 tags with relevance > 0.5
/// 4. Content coherence: summary makes sense linguistically
/// 5. No PII exposure: flag if personal data detected
/// Hybrid stack:
/// - Optional SK-based coherence scoring
/// - Polly retries at provider layer
/// </summary>
public class ValidationAgent : IValidationAgent
{
    private readonly ILLMProvider _llmProvider; // SK-backed adapter (optional)
    private readonly IContentModerationService _moderationService;
    private readonly ILogger<ValidationAgent> _logger;

    public string Name => "ValidationAgent";
    public AgentCapability Capability => AgentCapability.Validation;

    public async Task<AgentResult> ExecuteAsync(AgentContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var issues = new List<ValidationIssue>();
        var warnings = new List<string>();

        // TODO: Extract SummarizationResult, CategorizationResult, TaggingResult from context

        // Validation Check 1: Summary quality
        if (context.ContentText.Length < 50)
        {
            issues.Add(new(
                Category: "summary",
                Message: "Summary is too short",
                IsCritical: false,
                Suggestion: "Consider generating a longer summary"));
        }

        // Validation Check 2: Categorization confidence
        // (Add actual checks here with extracted results)

        // Validation Check 3: Tags quality
        // (Add tag validation)

        // Validation Check 4: Content coherence
        var coherenceScore = await _moderationService.CheckCoherenceAsync(context.ContentText);
        if (coherenceScore < 0.6)
        {
            warnings.Add("Summary coherence is low; consider manual review");
        }

        // Validation Check 5: PII detection
        var hasPII = await _moderationService.DetectPIIAsync(context.ContentText);
        if (hasPII)
        {
            issues.Add(new(
                Category: "security",
                Message: "Potential PII detected",
                IsCritical: true,
                Suggestion: "Redact personal data before storing"));
        }

        stopwatch.Stop();

        var status = issues.Any(i => i.IsCritical)
            ? ValidationStatus.Rejected
            : warnings.Count > 0
                ? ValidationStatus.ApprovedWithWarnings
                : ValidationStatus.Approved;

        return new AgentResult(
            Success: status != ValidationStatus.Rejected,
            Message: $"Validation {status}",
            Data: new(
                AgentName: Name,
                ExecutedAt: DateTimeOffset.UtcNow,
                Payload: new Dictionary<string, object>
                {
                    { "status", status },
                    { "issues", issues },
                    { "warnings", warnings }
                }),
            Metrics: new(0, 0m, stopwatch.Elapsed, 0, "validation"));
    }
}
```

---

## 3. Orchestration Pattern

### 3.1 Orchestrator Agent (Coordinator)

```csharp
namespace InfoDumpManager.Application.Agents.Orchestration;

/// <summary>
/// Central orchestrator that manages the multi-agent workflow.
/// Coordinates summarization → categorization → tagging → validation.
/// </summary>
public interface IContentProcessingOrchestrator
{
    Task<ProcessingResult> ProcessGEMAsync(
        Guid gemId,
        Guid tenantId,
        string contentText,
        ProcessingOptions options);

    Task<ProcessingResult> ProcessBatchAsync(
        IEnumerable<(Guid GEMId, Guid TenantId, string ContentText)> items,
        ProcessingOptions options);

    Task<JobStatus> GetJobStatusAsync(Guid jobId);
    
    IAsyncEnumerable<JobStatusUpdate> WatchJobAsync(Guid jobId);
}

public record ProcessingResult(
    Guid GEMId,
    ProcessingStatus Status,
    GEMSummary? Summary,
    CategorizationResult? Categorization,
    TaggingResult? Tags,
    ValidationResult? Validation,
    List<string> Errors,
    DateTimeOffset CompletedAt);

public enum ProcessingStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Implementation uses state machine pattern with agents as transitions.
/// </summary>
public class ContentProcessingOrchestrator : IContentProcessingOrchestrator
{
    private readonly ISummarizationAgent _summarizationAgent;
    private readonly ICategorizationAgent _categorizationAgent;
    private readonly ITaggingAgent _taggingAgent;
    private readonly IValidationAgent _validationAgent;
    private readonly IJobQueue<ProcessingJob> _jobQueue;
    private readonly IEventPublisher _eventPublisher;
    private readonly IGEMRepository _gemRepository;
    private readonly ILogger<ContentProcessingOrchestrator> _logger;

    public async Task<ProcessingResult> ProcessGEMAsync(
        Guid gemId,
        Guid tenantId,
        string contentText,
        ProcessingOptions options)
    {
        var jobId = Guid.NewGuid();
        var startTime = DateTimeOffset.UtcNow;
        var errors = new List<string>();
        GEMSummary? summary = null;
        CategorizationResult? categorization = null;
        TaggingResult? tags = null;
        ValidationResult? validation = null;

        try
        {
            _logger.LogInformation($"Starting processing for GEM {gemId}");

            // STAGE 1: Summarization
            _eventPublisher.PublishJobStatusChanged(
                jobId, ProcessingStatus.Processing, "Summarizing content...");

            var summaryContext = new AgentContext(
                GEMId: gemId,
                TenantId: tenantId,
                ContentText: contentText,
                Metadata: new(
                    ContentSource: options.Source,
                    EstimatedTokenCount: EstimateTokens(contentText),
                    CreatedAt: DateTimeOffset.UtcNow,
                    CustomData: new()));

            var summaryResult = await _summarizationAgent.ExecuteAsync(summaryContext);

            if (!summaryResult.Success)
            {
                errors.AddRange(summaryResult.Errors ?? new());
                throw new ProcessingException("Summarization failed");
            }

            summary = ExtractSummaryFromResult(summaryResult);
            _eventPublisher.PublishJobProgress(jobId, 33, "Summarization complete");

            // STAGE 2: Categorization
            _eventPublisher.PublishJobStatusChanged(
                jobId, ProcessingStatus.Processing, "Categorizing content...");

            var categorizationContext = new AgentContext(
                GEMId: gemId,
                TenantId: tenantId,
                ContentText: summary.Text,
                Metadata: new(
                    ContentSource: "summarization_output",
                    EstimatedTokenCount: 200,
                    CreatedAt: DateTimeOffset.UtcNow,
                    CustomData: new()));

            var categorizationResult = await _categorizationAgent.ExecuteAsync(categorizationContext);

            if (!categorizationResult.Success)
            {
                errors.AddRange(categorizationResult.Errors ?? new());
                // Don't fail - categorization can be suggested but not required
            }

            categorization = ExtractCategorizationFromResult(categorizationResult);

            // Check if confidence is too low
            if (categorization.ConfidenceScore < 0.7 && options.AutoApproveThreshold >= 0.7)
            {
                _eventPublisher.PublishCategoryFlaggedForReview(jobId, categorization);
            }

            _eventPublisher.PublishJobProgress(jobId, 66, "Categorization complete");

            // STAGE 3: Tagging
            _eventPublisher.PublishJobStatusChanged(
                jobId, ProcessingStatus.Processing, "Generating tags...");

            var taggingContext = new AgentContext(
                GEMId: gemId,
                TenantId: tenantId,
                ContentText: summary.Text,
                Metadata: new(
                    ContentSource: "summarization_output",
                    EstimatedTokenCount: 150,
                    CreatedAt: DateTimeOffset.UtcNow,
                    CustomData: new()));

            var taggingResult = await _taggingAgent.ExecuteAsync(taggingContext);

            if (!taggingResult.Success)
            {
                errors.AddRange(taggingResult.Errors ?? new());
            }

            tags = ExtractTaggingFromResult(taggingResult);
            _eventPublisher.PublishJobProgress(jobId, 85, "Tagging complete");

            // STAGE 4: Validation
            if (options.RunValidation)
            {
                _eventPublisher.PublishJobStatusChanged(
                    jobId, ProcessingStatus.Processing, "Validating results...");

                var validationContext = new AgentContext(
                    GEMId: gemId,
                    TenantId: tenantId,
                    ContentText: contentText,
                    Metadata: new(
                        ContentSource: "multi_agent_results",
                        EstimatedTokenCount: 0,
                        CreatedAt: DateTimeOffset.UtcNow,
                        CustomData: new()));

                var validationResult = await _validationAgent.ExecuteAsync(validationContext);
                validation = ExtractValidationFromResult(validationResult);
            }

            _eventPublisher.PublishJobProgress(jobId, 100, "Processing complete");

            // Persist results to database
            await PersistResultsAsync(gemId, tenantId, summary, categorization, tags);

            _logger.LogInformation($"Processing completed for GEM {gemId}");

            return new ProcessingResult(
                GEMId: gemId,
                Status: ProcessingStatus.Completed,
                Summary: summary,
                Categorization: categorization,
                Tags: tags,
                Validation: validation,
                Errors: errors,
                CompletedAt: DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Processing failed for GEM {gemId}: {ex.Message}");
            
            errors.Add(ex.Message);

            return new ProcessingResult(
                GEMId: gemId,
                Status: ProcessingStatus.Failed,
                Summary: summary,
                Categorization: categorization,
                Tags: tags,
                Validation: validation,
                Errors: errors,
                CompletedAt: DateTimeOffset.UtcNow);
        }
    }

    public async Task<ProcessingResult> ProcessBatchAsync(
        IEnumerable<(Guid GEMId, Guid TenantId, string ContentText)> items,
        ProcessingOptions options)
    {
        var batchId = Guid.NewGuid();
        var allResults = new List<ProcessingResult>();

        _logger.LogInformation($"Starting batch processing with {items.Count()} items");

        // Process with concurrency limit
        var concurrencyLimit = options.MaxConcurrentJobs ?? 3;
        var semaphore = new SemaphoreSlim(concurrencyLimit);

        var tasks = items.Select(async item =>
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
        });

        var results = await Task.WhenAll(tasks);
        
        _eventPublisher.PublishBatchComplete(batchId, results);

        return new ProcessingResult(
            GEMId: Guid.Empty, // Batch result
            Status: ProcessingStatus.Completed,
            Errors: results.Where(r => r.Status == ProcessingStatus.Failed)
                .SelectMany(r => r.Errors)
                .ToList(),
            CompletedAt: DateTimeOffset.UtcNow);
    }

    private int EstimateTokens(string text) => (int)(text.Length / 4.0);

    private async Task PersistResultsAsync(
        Guid gemId,
        Guid tenantId,
        GEMSummary summary,
        CategorizationResult categorization,
        TaggingResult tags)
    {
        var gem = await _gemRepository.GetByIdAsync(gemId);
        
        if (gem != null)
        {
            gem.UpdateSummary(summary);
            
            if (categorization?.SuggestedCategoryId.HasValue == true)
            {
                // Auto-assign category if confidence high enough
                // (simplified - implement actual logic)
            }

            // Store tags in database
            await _tagRepository.SaveTagsAsync(gemId, tags.GeneratedTags);

            // Store embedding in pgvector
            await _vectorStore.StoreEmbeddingAsync(gemId, tags.Embedding);

            await _gemRepository.UpdateAsync(gem);
        }
    }
}

public record ProcessingOptions(
    string Source = "web",
    bool AutoApproveThreshold = 0.7,
    bool RunValidation = true,
    int? MaxConcurrentJobs = null,
    TimeSpan? Timeout = null);
```

### 3.2 Job Queue Implementation

```csharp
namespace InfoDumpManager.Application.Infrastructure.JobQueue;

/// <summary>
/// In-memory job queue for background processing.
/// Persists pending jobs to database for durability.
/// </summary>
public interface IJobQueue<T> where T : class
{
    Task EnqueueAsync(T job);
    Task<T?> DequeueAsync(TimeSpan timeout);
    Task MarkCompleteAsync(T job);
    Task MarkFailedAsync(T job, string error, int retryCount);
    IAsyncEnumerable<T> DequeueBatchAsync(int batchSize);
}

public record ProcessingJob(
    Guid JobId,
    Guid GEMId,
    Guid TenantId,
    string ContentText,
    ProcessingOptions Options,
    int RetryCount = 0,
    DateTimeOffset CreatedAt = default,
    DateTimeOffset? StartedAt = null);

public class InMemoryJobQueue<T> : IJobQueue<T> where T : class
{
    private readonly Channel<T> _channel;
    private readonly ILogger<InMemoryJobQueue<T>> _logger;

    public InMemoryJobQueue(ILogger<InMemoryJobQueue<T>> logger)
    {
        _channel = Channel.CreateUnbounded<T>();
        _logger = logger;
    }

    public async Task EnqueueAsync(T job)
    {
        await _channel.Writer.WriteAsync(job);
        _logger.LogInformation($"Job enqueued: {job?.GetHashCode()}");
    }

    public async Task<T?> DequeueAsync(TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            return await _channel.Reader.ReadAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    public async Task MarkCompleteAsync(T job)
    {
        _logger.LogInformation($"Job completed: {job?.GetHashCode()}");
        await Task.CompletedTask;
    }

    public async Task MarkFailedAsync(T job, string error, int retryCount)
    {
        _logger.LogError($"Job failed (retry {retryCount}): {error}");
        
        // Retry logic with exponential backoff
        if (retryCount < 3)
        {
            var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount));
            await Task.Delay(delay);
            await EnqueueAsync(job);
        }
        else
        {
            _logger.LogError($"Job abandoned after {retryCount} retries");
        }
    }

    public async IAsyncEnumerable<T> DequeueBatchAsync(int batchSize)
    {
        for (int i = 0; i < batchSize; i++)
        {
            var job = await DequeueAsync(TimeSpan.FromSeconds(5));
            if (job != null)
            {
                yield return job;
            }
        }
    }
}
```

### 3.3 Background Processing Service

```csharp
namespace InfoDumpManager.Application.Services;

/// <summary>
/// Hosted service that continuously processes jobs from queue.
/// Implements graceful shutdown and memory leak prevention.
/// </summary>
public class ContentProcessingBackgroundService : BackgroundService
{
    private readonly IJobQueue<ProcessingJob> _jobQueue;
    private readonly IContentProcessingOrchestrator _orchestrator;
    private readonly ILogger<ContentProcessingBackgroundService> _logger;

    public ContentProcessingBackgroundService(
        IJobQueue<ProcessingJob> jobQueue,
        IContentProcessingOrchestrator orchestrator,
        ILogger<ContentProcessingBackgroundService> logger)
    {
        _jobQueue = jobQueue;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Content Processing Background Service started");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Dequeue and process jobs
                    var job = await _jobQueue.DequeueAsync(TimeSpan.FromSeconds(30));

                    if (job != null)
                    {
                        try
                        {
                            var result = await _orchestrator.ProcessGEMAsync(
                                job.GEMId,
                                job.TenantId,
                                job.ContentText,
                                job.Options);

                            if (result.Status == ProcessingStatus.Completed)
                            {
                                await _jobQueue.MarkCompleteAsync(job);
                            }
                            else if (result.Status == ProcessingStatus.Failed)
                            {
                                await _jobQueue.MarkFailedAsync(
                                    job,
                                    string.Join(", ", result.Errors),
                                    job.RetryCount + 1);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Error processing job: {ex.Message}");
                            await _jobQueue.MarkFailedAsync(job, ex.Message, job.RetryCount + 1);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Unexpected error in background service: {ex.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }
        finally
        {
            _logger.LogInformation("Content Processing Background Service stopped");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Content Processing Background Service stopping");
        await base.StopAsync(cancellationToken);
    }
}
```

---

## 4. Data Flow

### 4.1 GEM Ingestion → Processing → Storage

```
1. Browser Extension / API Endpoint
   └─> POST /api/gems (with content)

2. GEM Creation (Domain)
   └─> GEM aggregate root created with initial state

3. Queue Submission
   └─> ProcessingJob enqueued to IJobQueue

4. Background Service Dequeues
   ├─> Calls ContentProcessingOrchestrator
   │
   └─> Multi-Agent Pipeline
       ├─ SummarizationAgent.ExecuteAsync()
       │  └─ LLM Call (OpenAI API)
       │  └─ Generates: GEMSummary
       │
       ├─ CategorizationAgent.ExecuteAsync()
       │  └─ Embedding + Semantic Match
       │  └─ LLM Call (optional)
       │  └─ Generates: CategorizationResult
       │
       ├─ TaggingAgent.ExecuteAsync()
       │  └─ LLM Call + NER
       │  └─ Embedding generation
       │  └─ Generates: TaggingResult
       │
       └─ ValidationAgent.ExecuteAsync()
          └─ Quality checks
          └─ Generates: ValidationResult

5. Results Persisted
   ├─> GEM.Summary updated
   ├─> GEM.Category assigned (if high confidence)
   ├─> Tags stored in Tag table
   ├─> Embeddings stored in pgvector
   └─> Activity log recorded

6. Events Published
   └─> IEventPublisher.PublishProcessingComplete()
       └─> WebSocket → UI notifications

7. User sees results in UI
   ├─ AI summary displayed prominently
   ├─ Category suggestion shown with confidence
   ├─ Auto-accept if > threshold, else ask for confirmation
   └─ Related items recommended via semantic search
```

---

## 5. Domain Events

```csharp
namespace InfoDumpManager.Domain.Events;

/// <summary>
/// Domain events emitted during AI processing.
/// Consumed by application services and web UI.
/// </summary>

public sealed record GEMCreatedAndQueuedForProcessing(
    Guid GEMId,
    Guid TenantId,
    string Title,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record GEMSummarizationStarted(
    Guid GEMId,
    Guid TenantId,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record GEMSummarizationCompleted(
    Guid GEMId,
    Guid TenantId,
    string Summary,
    int TokensUsed,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record GEMCategorizationSuggested(
    Guid GEMId,
    Guid? CategoryId,
    double ConfidenceScore,
    bool RequiresManualReview,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record GEMProcessingCompleted(
    Guid GEMId,
    Guid TenantId,
    ProcessingResult Result,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record GEMProcessingFailed(
    Guid GEMId,
    Guid TenantId,
    List<string> Errors,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record CategorySuggestionRejectedByUser(
    Guid GEMId,
    Guid? SuggestedCategoryId,
    Guid? ActualCategoryId,
    DateTimeOffset OccurredAt) : IDomainEvent;
```

---

## 6. LLM Provider Abstraction

```csharp
namespace InfoDumpManager.Application.Services.LLM;

/// <summary>
/// Abstraction for LLM providers to enable swapping implementations.
/// Infrastructure layer implements this using Semantic Kernel + Polly.
/// </summary>
public interface ILLMProvider
{
    Task<LLMResponse> CallAsync(
        string prompt,
        string model = "gpt-4",
        int maxTokens = 500,
        float temperature = 0.7f,
        CancellationToken cancellationToken = default);

    Task<LLMResponse[]> CallBatchAsync(
        IEnumerable<string> prompts,
        string model = "gpt-4",
        int maxTokens = 500);

    bool IsAvailable { get; }
    string ProviderName { get; }
}

public record LLMResponse(
    string Content,
    int TokensUsed,
    int InputTokens,
    int OutputTokens,
    decimal CostEstimate,
    string Model,
    string Provider,
    string FinishReason,
    DateTimeOffset GeneratedAt);

/// <summary>
/// Semantic Kernel-backed provider with Polly resilience.
/// </summary>
public class SemanticKernelProvider : ILLMProvider
{
    private readonly IKernel _kernel;
    private readonly IAsyncPolicy _resiliencePolicy;
    private readonly ILogger<SemanticKernelProvider> _logger;

    public string ProviderName => "SemanticKernel";
    public bool IsAvailable => true;

    public async Task<LLMResponse> CallAsync(
        string prompt,
        string model = "gpt-4",
        int maxTokens = 500,
        float temperature = 0.7f,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _resiliencePolicy.ExecuteAsync(async () =>
            {
                var result = await _kernel.RunAsync(
                    new KernelArguments
                    {
                        ["input"] = prompt,
                        ["model"] = model,
                        ["maxTokens"] = maxTokens,
                        ["temperature"] = temperature
                    },
                    cancellationToken);

                return MapToLLMResponse(result);
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError($"LLM call failed: {ex.Message}");
            throw new LLMProviderException("LLM provider call failed", isRetryable: true, ex);
        }
    }
}
```

---

## 7. Embedding Service

```csharp
namespace InfoDumpManager.Application.Services.Embeddings;

/// <summary>
/// Abstraction for embedding generation and storage.
/// Enables vector similarity search via pgvector.
/// Kernel Memory can be used to manage embeddings and chunking (optional).
/// </summary>
public interface IEmbeddingProvider
{
    Task<float[]> GetEmbeddingAsync(
        string text,
        bool useCacheIfAvailable = true);

    Task<Dictionary<string, float[]>> GetEmbeddingsBatchAsync(
        IEnumerable<string> texts);
}

public interface IVectorStore
{
    Task StoreEmbeddingAsync(Guid gemId, float[] embedding);
    Task<List<(Guid GEMId, double Similarity)>> SearchSimilarAsync(
        float[] embedding,
        int topK = 10);
}

public class RedisEmbeddingCache : IEmbeddingCache
{
    private readonly IConnectionMultiplexer _redis;

    public async Task<float[]?> GetAsync(string text)
    {
        var db = _redis.GetDatabase();
        var key = $"embedding:{Hash(text)}";
        var cached = await db.StringGetAsync(key);

        if (cached.IsNullOrEmpty)
            return null;

        return JsonSerializer.Deserialize<float[]>(cached.ToString());
    }

    public async Task SetAsync(string text, float[] embedding, TimeSpan? ttl = null)
    {
        var db = _redis.GetDatabase();
        var key = $"embedding:{Hash(text)}";
        var json = JsonSerializer.Serialize(embedding);

        await db.StringSetAsync(key, json, ttl ?? TimeSpan.FromDays(30));
    }
}

public class PostgreSQLVectorStore : IVectorStore
{
    private readonly IDbContextFactory<InfoDumpManagerDbContext> _contextFactory;

    public async Task StoreEmbeddingAsync(Guid gemId, float[] embedding)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        
        var vector = new Vector { Embedding = embedding }; // pgvector type
        
        context.Update(new { GEMId = gemId, Vector = vector });
        await context.SaveChangesAsync();
    }

    public async Task<List<(Guid GEMId, double Similarity)>> SearchSimilarAsync(
        float[] embedding,
        int topK = 10)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var results = await context.GEMs
            .FromSql($@"
                SELECT g.Id, g.Vector <-> @embedding AS distance
                FROM gems g
                ORDER BY distance
                LIMIT {topK}
            ")
            .ToListAsync();

        return results.Select(r => (r.Id, 1 - r.Distance)).ToList(); // Convert distance to similarity
    }
}

/// <summary>
/// Kernel Memory-backed embedding provider (optional).
/// </summary>
public class KernelMemoryEmbeddingProvider : IEmbeddingProvider
{
    private readonly IKernelMemory _kernelMemory;

    public async Task<float[]> GetEmbeddingAsync(string text, bool useCacheIfAvailable = true)
    {
        return await _kernelMemory.Embeddings.GenerateAsync(text);
    }
}
```

---

## 8. Cost Management

```csharp
namespace InfoDumpManager.Application.Services.CostManagement;

public interface ICostManager
{
    Task<BudgetCheckResult> CanProcessAsync(Guid tenantId, int estimatedTokens);
    Task RecordUsageAsync(
        Guid tenantId,
        Guid gemId,
        string operationType,
        int tokensUsed,
        decimal cost);
    Task<TenantBudgetStats> GetBudgetStatsAsync(Guid tenantId);
    Task EnforceTokenBudgetAsync(Guid tenantId);
}

public record BudgetCheckResult(
    bool Allowed,
    string Reason,
    string Message);

public record TenantBudgetStats(
    Guid TenantId,
    decimal TotalTokensUsed,
    decimal TotalCost,
    decimal DailyBudget,
    decimal MonthlyBudget,
    decimal RemainingBudget);

public class CostManagerImpl : ICostManager
{
    private readonly ICostUsageRepository _costRepository;
    private readonly ITenantSettingsRepository _settingsRepository;
    private readonly ILogger<CostManagerImpl> _logger;

    public async Task<BudgetCheckResult> CanProcessAsync(Guid tenantId, int estimatedTokens)
    {
        var settings = await _settingsRepository.GetSettingsAsync(tenantId);
        var stats = await GetBudgetStatsAsync(tenantId);

        var estimatedCost = estimatedTokens * 0.00002m; // GPT-4 pricing (simplified)

        if (stats.RemainingBudget - estimatedCost < 0)
        {
            return new BudgetCheckResult(
                Allowed: false,
                Reason: "InsufficientBudget",
                Message: $"Insufficient budget. Estimated cost: ${estimatedCost}, Remaining: ${stats.RemainingBudget}");
        }

        return new BudgetCheckResult(
            Allowed: true,
            Reason: "OK",
            Message: "Budget check passed");
    }

    public async Task RecordUsageAsync(
        Guid tenantId,
        Guid gemId,
        string operationType,
        int tokensUsed,
        decimal cost)
    {
        var usage = new CostUsage
        {
            TenantId = tenantId,
            GEMId = gemId,
            OperationType = operationType,
            TokensUsed = tokensUsed,
            Cost = cost,
            RecordedAt = DateTimeOffset.UtcNow
        };

        await _costRepository.AddAsync(usage);
    }
}
```

---

## 9. Implementation Phases

### Phase 1: Foundation (Weeks 1-2)
- [ ] Define Agent interfaces and contracts
- [ ] Implement Orchestrator pattern
- [ ] Set up job queue (in-memory)
- [ ] Configure LLM provider abstraction (Semantic Kernel + Polly)
- [ ] Configure MediatR for domain events
- [ ] Create domain events

### Phase 2: Core Agents (Weeks 3-5)
- [ ] Implement SummarizationAgent with fallback
- [ ] Implement CategorizationAgent with semantic matching
- [ ] Implement TaggingAgent with NER
- [ ] Implement ValidationAgent
- [ ] Add pgvector support for embeddings
- [ ] Integrate Kernel Memory for embeddings (optional)

### Phase 3: Integration (Weeks 6-7)
- [ ] Background service integration
- [ ] Error handling and retry logic
- [ ] Cost tracking and budget enforcement
- [ ] Activity logging and audit trail

### Phase 4: API & UI (Weeks 8-9)
- [ ] API endpoints for manual summarization/categorization
- [ ] WebSocket for real-time job updates
- [ ] Web UI for reviewing AI suggestions
- [ ] Admin dashboard for cost monitoring

### Phase 5: Testing & Optimization (Weeks 10-12)
- [ ] Unit tests for all agents
- [ ] Integration tests end-to-end
- [ ] Performance benchmarking
- [ ] Production hardening

---

## 10. Key Design Benefits

✅ **Separation of Concerns**: Each agent has a single, testable responsibility
✅ **Extensibility**: New agents can be added without modifying existing code
✅ **Resilience**: Agents can fail independently; fallbacks available
✅ **Observability**: All operations logged with full audit trail
✅ **Cost Management**: Embedded cost tracking and budget enforcement
✅ **Async-First**: Background processing ensures responsive UI
✅ **Multi-Tenant**: Built-in tenant isolation and security
✅ **Testability**: Agents can be mocked and tested in isolation
✅ **Provider Agnostic**: LLM providers can be swapped without code changes
✅ **Graceful Degradation**: Quality fallbacks when primary methods fail

---

## 11. Metrics & Observability

Each agent emits:
- **Execution time**: P50, P95, P99 latencies
- **Success rate**: Percentage of successful executions
- **Token usage**: Input + output tokens per operation
- **Cost**: Estimated cost per operation + cumulative costs
- **Confidence scores**: Quality metrics for suggestions
- **Error rates**: Failures and fallback activations

Dashboards:
- Admin: Cost tracking, token usage, provider health
- User: Processing status, job history, confidence indicators
- Developer: Agent performance, error rates, API latencies

---

**Document Complete**
