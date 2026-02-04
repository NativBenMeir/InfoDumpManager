---
goal: Complete Implementation Plan for Partially Implemented AI & Search Features
version: 1.0
date_created: 2026-02-04
last_updated: 2026-02-04
owner: Development Team
status: 'In progress'
tags: [feature, ai-completion, vector-search, tagging, synthesis, upgrade]
---

# Introduction

![Status: In progress](https://img.shields.io/badge/status-In%20progress-yellow)

This implementation plan focuses on **completing partially implemented features** identified in the architecture review of arch.md and implementation-plan-1.md. The codebase has established the foundational infrastructure, domain models, and API endpoints, but seven critical features remain incomplete:

1. **F2: AI-Powered Summarization** - Logic stubs exist, actual LLM execution needed
2. **F3: Intelligent Auto-Categorization** - Infrastructure ready, categorization algorithm pending
3. **F4: AI Tag Suggestion & Management** - Embedding infrastructure defined, tagging logic missing
4. **F6: GEM Discovery & Search** - Basic CRUD works, full-text and semantic search incomplete
5. **F7: Category-Level Synthesis & Q&A** - Domain events defined, Q&A engine not implemented
6. **TE4: Vector Database Integration** - pgvector configured, entity mapping and search missing
7. **TE6: Object Storage Service** - MinIO client exists, lifecycle policies and pre-signed URLs pending

This plan targets **2-3 weeks of focused development** to achieve full feature parity with the architecture specification. The work is structured in three sequential phases that can be executed in parallel by different developers where dependencies permit.

---

## 1. Requirements & Constraints

### Functional Requirements

- **REQ-001**: Summarization agent must call LLM provider and store generated summaries linked to GEMs
- **REQ-002**: Categorization agent must analyze content + suggest categories with confidence scores
- **REQ-003**: Tagging agent must generate semantic embeddings and suggest relevant tags
- **REQ-004**: Full-text search must support keyword matching across GEM titles and summaries
- **REQ-005**: Semantic search must use pgvector similarity with configurable distance metric
- **REQ-006**: Hybrid search must combine full-text and semantic results with unified ranking
- **REQ-007**: Q&A engine must retrieve relevant GEMs and generate grounded answers with citations
- **REQ-008**: Category synthesis must generate comprehensive summaries of all GEMs in category
- **REQ-009**: Vector embeddings must be generated for text > 10 characters, cached to avoid duplication
- **REQ-010**: Search results must support filtering by category, tags, and date range simultaneously

### Non-Functional Requirements

- **NFR-001**: Summarization must complete within 10 seconds (p95) for typical web pages (< 5KB)
- **NFR-002**: Categorization must return suggestions within 3 seconds (p95)
- **NFR-003**: Semantic search must return top-10 results within 500ms (p95)
- **NFR-004**: Tag suggestion must complete within 5 seconds (p95) for new GEMs
- **NFR-005**: Q&A synthesis must generate answers within 8 seconds (p95)
- **NFR-006**: Support searching across 1,000+ GEMs with minimal latency degradation
- **NFR-007**: Embedding cache must reduce duplicate API calls by 90%

### Data Requirements

- **DATA-001**: GEM table must have vector columns: title_embedding (1536D), summary_embedding (1536D)
- **DATA-002**: Vector indices must be created with HNSW method for optimal search performance
- **DATA-003**: Embedding metadata must track: model_name, token_count, created_at, cache_hit
- **DATA-004**: Search logs must capture: query_text, search_mode, filters_used, result_count, latency_ms

### Security Requirements

- **SEC-001**: LLM API keys must not be logged or exposed in error messages
- **SEC-002**: Vector embeddings must respect multi-tenant isolation (not shared across tenants)
- **SEC-003**: Search queries must be sanitized to prevent injection attacks
- **SEC-004**: Pre-signed URLs from MinIO must expire within 1 hour maximum

### Architectural Constraints

- **CON-001**: All AI agents must use established ILLMProvider interface (no new abstractions)
- **CON-002**: Vector operations must use Npgsql.EntityFrameworkCore.PostgreSQL pgvector support exclusively
- **CON-003**: Search logic must reside in repositories, not controllers (separation of concerns)
- **CON-004**: Embedding generation must happen asynchronously in background services
- **CON-005**: MinIO integration must support S3-compatible APIs (not vendor-specific features)
- **CON-006**: All new features must integrate with existing ActivityLog event tracking

### Development Guidelines

- **GUD-001**: All agent implementations must have 100% unit test coverage (mock LLM responses)
- **GUD-002**: Integration tests must use Testcontainers for PostgreSQL + pgvector testing
- **GUD-003**: Search tests must validate ranking quality with synthetic test data
- **GUD-004**: Agent tests must use deterministic LLM responses (Moq mocking)
- **GUD-005**: All vector operations must handle null/missing embeddings gracefully
- **GUD-006**: Search filters must support null values (e.g., optional category filter)
- **GUD-007**: Q&A responses must include source GEM references with excerpt quotes

### Design Patterns

- **PAT-001**: Agent pattern - Each agent (Summarization, Categorization, Tagging) inherits from IAgent interface
- **PAT-002**: Strategy pattern - Search module supports plug-gable search strategies (full-text, semantic, hybrid)
- **PAT-003**: Repository pattern - All data queries encapsulated in IGEMRepository and extensions
- **PAT-004**: Bag of embeddings pattern - Store embeddings alongside text to support future RAG scenarios
- **PAT-005**: Cache-aside pattern - Check Redis first for embeddings, compute if miss, store result

---

## 2. Implementation Steps

### Implementation Phase 1: AI Agents Completion (5-6 days)

**GOAL-001**: Fully implement Summarization, Categorization, and Tagging agents with LLM integration and event publishing

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Complete SummarizationAgent.cs with actual LLM call using SemanticKernelProvider | | |
| TASK-002 | Implement GEMSummary creation and persistence in summarization agent | | |
| TASK-003 | Add error handling and retry logic for summarization failures in background service | | |
| TASK-004 | Emit GEMSummarizationCompleted domain event after successful summarization | | |
| TASK-005 | Complete CategorizationAgent.cs with category suggestion algorithm | | |
| TASK-006 | Implement category analysis logic: fetch existing categories, call LLM, parse response | | |
| TASK-007 | Implement confidence score calculation for categorization suggestions | | |
| TASK-008 | Emit GEMCategorizationSuggested domain event with suggested category ID and confidence | | |
| TASK-009 | Add auto-assignment logic if confidence >= configurable threshold (default 0.8) | | |
| TASK-010 | Implement user override mechanism to reject/change AI suggestion | | |
| TASK-011 | Complete TaggingAgent.cs with embedding-based tag suggestion | | |
| TASK-012 | Implement embedding generation for tag suggestion (call embedding provider) | | |
| TASK-013 | Implement tag suggestion algorithm: semantic similarity search in tag embeddings | | |
| TASK-014 | Cache tag suggestions to prevent duplicate computation (Redis with 24h TTL) | | |
| TASK-015 | Emit GEMTaggingSuggested domain event with suggested tags and similarity scores | | |
| TASK-016 | Update activitylog entries for all agent operations with metadata (model, tokens, confidence) | | |
| TASK-017 | Implement ValidationAgent as pre-processing step before summarization | | |
| TASK-018 | Add rate limiting per-tenant for LLM calls using Polly rate limiter | | |
| TASK-019 | Implement graceful fallback for mocked LLM responses (deterministic for testing) | | |
| TASK-020 | Write comprehensive unit tests for all agents using Moq for LLM provider | | |
| TASK-AUT | Implement all unit tests based on Testing section in this plan | | |
| TASK-AIT | Implement all integration tests based on Testing section in this plan | | |

### Implementation Phase 2: Vector Database & Semantic Search (6-7 days)

**GOAL-002**: Implement pgvector integration, embedding generation pipeline, and hybrid search functionality

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-021 | Add vector columns to GEM entity: TitleEmbedding, SummaryEmbedding (pgvector type) | | |
| TASK-022 | Create EF Core migration for vector columns with pgvector index (HNSW method) | | |
| TASK-023 | Configure pgvector EF Core extension in ApplicationDbContext with IVectorStore implementation | | |
| TASK-024 | Implement IEmbeddingProvider interface using SemanticKernelProvider for OpenAI embeddings | | |
| TASK-025 | Implement embedding generation service that caches results in Redis | | |
| TASK-026 | Create cache key strategy for embeddings: hash(tenant_id + text_content) | | |
| TASK-027 | Implement background job to generate embeddings for existing GEMs (backfill) | | |
| TASK-028 | Modify CreateGEMCommand to generate embeddings asynchronously after summarization | | |
| TASK-029 | Implement vector similarity search method in IGEMRepository (cosine distance) | | |
| TASK-030 | Implement full-text search method in IGEMRepository using PostgreSQL FTS | | |
| TASK-031 | Implement hybrid search combining full-text and vector results with unified ranking | | |
| TASK-032 | Create ranking algorithm: score = weight_text * text_relevance + weight_vector * vector_similarity | | |
| TASK-033 | Implement search with complex filters: category_id, tag_ids[], date_range, search_mode | | |
| TASK-034 | Create SearchQuery MediatR command and handler for search orchestration | | |
| TASK-035 | Create Search API endpoint: GET /api/v1/search with query, filters, pagination | | |
| TASK-036 | Implement pagination for search results with has_more flag | | |
| TASK-037 | Create search results DTO with relevance score and search highlights | | |
| TASK-038 | Write integration tests for vector similarity search with synthetic data | | |
| TASK-039 | Write tests for full-text search with various keyword combinations | | |
| TASK-040 | Write tests for hybrid search ranking algorithm | | |
| TASK-041 | Benchmark vector search performance and optimize HNSW parameters | | |
| TASK-042 | Create database index for common filter combinations (category_id, created_at) | | |
| TASK-043 | Implement query result caching in Redis for frequently searched terms | | |
| TASK-044 | Add search analytics logging: capture all searches for usage insights | | |

### Implementation Phase 3: Q&A Synthesis & Polish (5-6 days)

**GOAL-003**: Implement category synthesis and Q&A engine with source citation, complete MinIO functionality, and finalize search UI

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-045 | Design and implement category synthesis algorithm using RAG pattern | | |
| TASK-046 | Implement category summarization prompt: analyze GEMs → generate comprehensive summary | | |
| TASK-047 | Create CategorySynthesisQuery MediatR query and handler | | |
| TASK-048 | Implement synthesis caching: store results with 24h TTL (configurable) | | |
| TASK-049 | Create Synthesize API endpoint: POST /api/v1/categories/{id}/synthesize | | |
| TASK-050 | Design and implement Q&A engine prompt template with source citation format | | |
| TASK-051 | Implement RAG retrieval: semantic search for top-10 most relevant GEMs to question | | |
| TASK-052 | Implement answer generation with source citation formatting | | |
| TASK-053 | Create QuestionAnswerQuery MediatR query and handler | | |
| TASK-054 | Create Ask API endpoint: POST /api/v1/categories/{id}/ask with user question | | |
| TASK-055 | Implement feedback mechanism: POST /api/v1/answers/{id}/feedback with sentiment (up/down) | | |
| TASK-056 | Store feedback in database for future model training and quality metrics | | |
| TASK-057 | Emit GEMSynthesisCompleted event after successful synthesis | | |
| TASK-058 | Emit GEMQAAsked event after Q&A interaction with feedback metadata | | |
| TASK-059 | Implement MinIO pre-signed URL generation for secure snapshot access | | |
| TASK-060 | Configure MinIO URL expiration: 1 hour default, customizable per request | | |
| TASK-061 | Implement retention policies for MinIO snapshots: auto-delete after 90 days (configurable) | | |
| TASK-062 | Create snapshot versioning strategy: store multiple versions of same GEM snapshot | | |
| TASK-063 | Implement snapshot retrieval with fallback for missing objects | | |
| TASK-064 | Create search UI page component with semantic highlighting of matches | | |
| TASK-065 | Implement search filter UI: category dropdown, tag multi-select, date range picker | | |
| TASK-066 | Add search mode selection UI: full-text vs semantic vs hybrid | | |
| TASK-067 | Create category view page with synthesis results display | | |
| TASK-068 | Create Q&A chat interface with streaming response display | | |
| TASK-069 | Implement loading states and error handling for all async operations | | |
| TASK-070 | Write end-to-end tests for complete search workflows | | |
| TASK-071 | Write integration tests for Q&A generation with mocked LLM responses | | |
| TASK-072 | Write tests for synthesis caching behavior | | |
| TASK-073 | Write tests for snapshot versioning and retention policies | | |
| TASK-074 | Optimize search and synthesis query performance with query analysis | | |
| TASK-075 | Create monitoring dashboards for search analytics and Q&A usage metrics | | |
| TASK-076 | Document search API with examples: full-text, semantic, hybrid searches | | |
| TASK-077 | Document Q&A API with example questions and answer format | | |
| TASK-078 | Document embedding generation and caching strategy | | |

---

## 3. Task Details

### Phase 1 Detailed Tasks: AI Agents Implementation

#### TASK-001: Complete SummarizationAgent with LLM Integration

**Current State:** `SummarizationAgent.cs` exists as stub  
**Target State:** Full implementation with LLM calls, error handling, event publishing  
**Files Affected:** [SummarizationAgent.cs](../../../src/InfoDumpManager.Application/Agents/Implementations/SummarizationAgent.cs)

**Implementation Steps:**
1. Inject `ILLMProvider` and `IGEMRepository` into constructor
2. Implement `ExecuteAsync()` method:
   - Retrieve GEM from repository
   - Extract content (title + snapshot HTML)
   - Call LLM provider with summarization prompt
   - Create GEMSummary value object with generated text
   - Update GEM entity with summary
   - Save to repository
   - Publish GEMSummarizationCompleted event
3. Add error handling: log failures, don't throw (background service resilience)
4. Implement token counting and cost tracking
5. Add cache check to avoid re-summarizing same content

**Test Coverage:**
- Test successful summarization with mocked LLM response
- Test LLM timeout handling (circuit breaker behavior)
- Test repository save failure handling
- Test event publishing
- Test cost calculation accuracy

**Code Example:**
```csharp
public class SummarizationAgent : IAgent
{
    private readonly ILLMProvider _llmProvider;
    private readonly IGEMRepository _gemRepository;
    private readonly ILogger<SummarizationAgent> _logger;

    public async Task<AgentResult> ExecuteAsync(AgentInput input, CancellationToken ct)
    {
        var gem = await _gemRepository.GetByIdAsync(input.GemId, ct);
        if (gem is null) return AgentResult.Failure("GEM not found");

        var prompt = $"Summarize the following content in 2-3 sentences:\n\n{gem.Title}\n{gem.Snapshot.Html}";
        var response = await _llmProvider.CompletionAsync(prompt, ct);

        var summary = new GEMSummary(
            response.Text,
            response.Model,
            response.TokenCount,
            DateTimeOffset.UtcNow
        );

        gem.UpdateSummary(summary);
        await _gemRepository.UpdateAsync(gem, ct);

        return AgentResult.Success(new { SummaryText = summary.Text });
    }
}
```

---

#### TASK-005: Complete CategorizationAgent with Category Suggestion Algorithm

**Current State:** `CategorizationAgent.cs` exists as stub  
**Target State:** Full implementation with category analysis, confidence calculation, event publishing  
**Files Affected:** [CategorizationAgent.cs](../../../src/InfoDumpManager.Application/Agents/Implementations/CategorizationAgent.cs)

**Implementation Steps:**
1. Inject `ILLMProvider`, `IGEMRepository`, `ICategoryRepository` into constructor
2. Implement category analysis algorithm:
   - Fetch all categories for tenant with summaries/descriptions
   - Extract GEM content (title + summary)
   - Create prompt: "Analyze this GEM and existing categories, suggest best category or propose new name"
   - Call LLM provider with category list + GEM content
   - Parse response to extract: suggested_category_id OR proposed_category_name, confidence_score
3. Store categorization metadata on GEM: `CategorySuggestedByAI`, `CategoryConfidenceScore`
4. Implement auto-assignment if confidence >= threshold (configurable, default 0.8)
5. Implement rejection mechanism to ignore low-confidence suggestions
6. Publish GEMCategorizationSuggested event with suggestion data
7. Log categorization rationale (which categories were analyzed, why one selected)

**Test Coverage:**
- Test categorization with 10+ existing categories
- Test confidence score calculation
- Test auto-assignment threshold enforcement
- Test category rejection workflow
- Test new category proposal parsing
- Test multi-tenant isolation (categories not shared)

**Code Example:**
```csharp
public class CategorizationAgent : IAgent
{
    public async Task<AgentResult> ExecuteAsync(AgentInput input, CancellationToken ct)
    {
        var gem = await _gemRepository.GetByIdAsync(input.GemId, ct);
        var categories = await _categoryRepository.GetAllForTenantAsync(gem.TenantId, ct);

        var categoryList = string.Join("\n", categories.Select(c => $"- {c.Name}: {c.Description}"));
        var prompt = $@"
Analyze this GEM and suggest the best category from the list below, or propose a new category name.

GEM:
Title: {gem.Title}
Summary: {gem.Summary?.Text}

Categories:
{categoryList}

Response format (JSON):
{{
  ""suggested_category_id"": ""guid-or-null"",
  ""proposed_category_name"": ""name-or-null"",
  ""confidence"": 0.0-1.0,
  ""rationale"": ""explanation""
}}";

        var response = await _llmProvider.CompletionAsync(prompt, ct);
        var suggestion = JsonSerializer.Deserialize<CategorySuggestion>(response.Text);

        if (suggestion.Confidence >= 0.8 && suggestion.SuggestedCategoryId.HasValue)
        {
            gem.AssignCategory(suggestion.SuggestedCategoryId.Value);
            await _gemRepository.UpdateAsync(gem, ct);
        }

        return AgentResult.Success(suggestion);
    }
}
```

---

#### TASK-011: Complete TaggingAgent with Embedding-Based Suggestion

**Current State:** `TaggingAgent.cs` exists as stub with embedding infrastructure references  
**Target State:** Full implementation with embedding generation, tag suggestion, caching  
**Files Affected:** [TaggingAgent.cs](../../../src/InfoDumpManager.Application/Agents/Implementations/TaggingAgent.cs)

**Implementation Steps:**
1. Inject `IEmbeddingProvider`, `IVectorStore`, `IGEMRepository`, `IEmbeddingCache` into constructor
2. Implement tag suggestion algorithm:
   - Generate embedding for GEM content (title + summary)
   - Retrieve cached embeddings for all existing tags (or generate if missing)
   - Calculate semantic similarity between GEM and each tag
   - Rank tags by similarity score
   - Return top-5 tags with similarity scores >= 0.7 threshold
3. Implement caching strategy:
   - Check Redis for GEM embedding before generating
   - Check Redis for tag embeddings (bulk cache miss = list to pre-compute)
   - Store new embeddings in Redis with 24h TTL
4. Implement error handling for embedding API failures (fallback to keyword matching if needed)
5. Publish GEMTaggingSuggested event with suggested tags and scores
6. Update activity log with tagging operation metadata

**Test Coverage:**
- Test embedding generation and caching (hit/miss scenarios)
- Test similarity scoring accuracy
- Test tag suggestion with 100+ existing tags
- Test threshold filtering
- Test embedding API failure fallback
- Test cache expiration and refresh

**Code Example:**
```csharp
public class TaggingAgent : IAgent
{
    public async Task<AgentResult> ExecuteAsync(AgentInput input, CancellationToken ct)
    {
        var gem = await _gemRepository.GetByIdAsync(input.GemId, ct);
        var content = $"{gem.Title} {gem.Summary?.Text}";

        // Try cache first
        var cacheKey = $"embedding:{gem.TenantId}:{HashContent(content)}";
        var embedding = await _embeddingCache.TryGetAsync(cacheKey, ct);

        if (embedding is null)
        {
            embedding = await _embeddingProvider.GenerateEmbeddingAsync(content, ct);
            await _embeddingCache.SetAsync(cacheKey, embedding, TimeSpan.FromDays(1), ct);
        }

        // Retrieve tag embeddings and calculate similarity
        var tagResults = await _vectorStore.FindSimilarAsync(embedding, topK: 10, ct);
        var suggestions = tagResults
            .Where(r => r.Similarity >= 0.7)
            .Take(5)
            .Select(r => new TagSuggestion(r.TagId, r.TagName, r.Similarity))
            .ToList();

        return AgentResult.Success(new { SuggestedTags = suggestions });
    }
}
```

---

### Phase 2 Detailed Tasks: Vector Database & Search

#### TASK-021 & TASK-022: Vector Column Addition and Migration

**Current State:** GEM entity has no vector columns  
**Target State:** GEM table has `TitleEmbedding` and `SummaryEmbedding` pgvector columns  
**Files Affected:**
- [GEM.cs](../../../src/InfoDumpManager.Domain/Entities/GEM.cs) - Add vector properties
- [ApplicationDbContext.cs](../../../src/InfoDumpManager.Infrastructure/Data/ApplicationDbContext.cs) - Configure pgvector
- New migration file

**Implementation Steps:**
1. Add vector properties to GEM entity:
   ```csharp
   public float[]? TitleEmbedding { get; set; }
   public float[]? SummaryEmbedding { get; set; }
   ```
2. Configure in DbContext:
   ```csharp
   modelBuilder.Entity<GEM>()
       .Property(g => g.TitleEmbedding)
       .HasColumnType("vector(1536)");
   
   modelBuilder.Entity<GEM>()
       .Property(g => g.SummaryEmbedding)
       .HasColumnType("vector(1536)");
   ```
3. Create migration: `dotnet ef migrations add AddVectorColumnsToGEM`
4. Add HNSW index in separate migration:
   ```sql
   CREATE INDEX idx_gem_summary_embedding ON gems 
   USING hnsw (summary_embedding vector_cosine_ops);
   ```
5. Verify migration applies successfully to test database

---

#### TASK-029: Implement Vector Similarity Search in Repository

**Current State:** IGEMRepository exists with basic CRUD  
**Target State:** Add semantic similarity search method  
**Files Affected:** [IGEMRepository.cs](../../../src/InfoDumpManager.Domain/Repositories/IGEMRepository.cs), [GEMRepository.cs](../../../src/InfoDumpManager.Infrastructure/Repositories/GEMRepository.cs)

**Implementation Steps:**
1. Add interface method to IGEMRepository:
   ```csharp
   Task<List<(GEM Gem, float Distance)>> SearchBySemanticSimilarityAsync(
       float[] queryEmbedding, 
       int topK = 10,
       float maxDistance = 0.5f,
       Guid? categoryFilter = null,
       CancellationToken ct = default);
   ```
2. Implement using EF Core with pgvector extension:
   ```csharp
   public async Task<List<(GEM, float)>> SearchBySemanticSimilarityAsync(
       float[] queryEmbedding, 
       int topK,
       float maxDistance,
       Guid? categoryFilter,
       CancellationToken ct)
   {
       var query = _context.GEMs
           .Where(g => g.TenantId == _tenantId && !g.IsDeleted)
           .Where(g => g.SummaryEmbedding != null)
           .Where(g => categoryFilter == null || g.CategoryId == categoryFilter);

       var results = await query
           .Select(g => new
           {
               Gem = g,
               Distance = g.SummaryEmbedding.CosineDistance(queryEmbedding)
           })
           .Where(r => r.Distance <= maxDistance)
           .OrderBy(r => r.Distance)
           .Take(topK)
           .ToListAsync(ct);

       return results.Select(r => (r.Gem, r.Distance)).ToList();
   }
   ```
3. Handle null embeddings gracefully (filter out before distance calculation)
4. Return results with both GEM and similarity score (1 - distance)

---

#### TASK-030: Implement Full-Text Search in Repository

**Current State:** No full-text search implementation  
**Target State:** PostgreSQL FTS-based keyword search  
**Files Affected:** [IGEMRepository.cs](../../../src/InfoDumpManager.Domain/Repositories/IGEMRepository.cs), [GEMRepository.cs](../../../src/InfoDumpManager.Infrastructure/Repositories/GEMRepository.cs)

**Implementation Steps:**
1. Add interface method:
   ```csharp
   Task<List<(GEM Gem, float Rank)>> SearchByFullTextAsync(
       string searchQuery,
       int topK = 10,
       Guid? categoryFilter = null,
       CancellationToken ct = default);
   ```
2. Implement using PostgreSQL FTS:
   ```csharp
   public async Task<List<(GEM, float)>> SearchByFullTextAsync(
       string searchQuery,
       int topK,
       Guid? categoryFilter,
       CancellationToken ct)
   {
       var query = _context.GEMs
           .Where(g => g.TenantId == _tenantId && !g.IsDeleted)
           .Where(g => categoryFilter == null || g.CategoryId == categoryFilter);

       var results = await query
           .Select(g => new
           {
               Gem = g,
               Rank = EF.Functions.TrigramsWordSimilarity(
                   searchQuery, 
                   g.Title + " " + (g.Summary != null ? g.Summary.Text : "")
               )
           })
           .Where(r => r.Rank > 0.1)  // Minimum relevance threshold
           .OrderByDescending(r => r.Rank)
           .Take(topK)
           .ToListAsync(ct);

       return results.Select(r => (r.Gem, r.Rank)).ToList();
   }
   ```
3. Add pg_trgm extension if not present (migration)
4. Return results ranked by relevance

---

#### TASK-031: Implement Hybrid Search with Unified Ranking

**Current State:** No hybrid search  
**Target State:** Combined full-text + semantic search with weighted ranking  
**Files Affected:** [IGEMRepository.cs](../../../src/InfoDumpManager.Domain/Repositories/IGEMRepository.cs), [GEMRepository.cs](../../../src/InfoDumpManager.Infrastructure/Repositories/GEMRepository.cs)

**Implementation Steps:**
1. Add interface method:
   ```csharp
   Task<List<(GEM Gem, float RelevanceScore)>> SearchHybridAsync(
       string searchQuery,
       float[] queryEmbedding,
       float textWeight = 0.4f,
       float vectorWeight = 0.6f,
       int topK = 10,
       Guid? categoryFilter = null,
       CancellationToken ct = default);
   ```
2. Execute both searches in parallel:
   ```csharp
   var textTask = SearchByFullTextAsync(searchQuery, topK * 2, categoryFilter, ct);
   var vectorTask = SearchBySemanticSimilarityAsync(queryEmbedding, topK * 2, 1.0f, categoryFilter, ct);
   await Task.WhenAll(textTask, vectorTask);
   ```
3. Combine results with unified scoring:
   ```csharp
   var textResults = textTask.Result.ToDictionary(r => r.Gem.Id, r => r.Rank);
   var vectorResults = vectorTask.Result.ToDictionary(r => r.Gem.Id, r => 1 - r.Distance);
   
   var allGemIds = textResults.Keys.Union(vectorResults.Keys);
   var combined = allGemIds.Select(id =>
   {
       var textScore = textResults.TryGetValue(id, out var tScore) ? tScore : 0f;
       var vectorScore = vectorResults.TryGetValue(id, out var vScore) ? vScore : 0f;
       var relevance = (textScore * textWeight) + (vectorScore * vectorWeight);
       
       var gem = textResults.ContainsKey(id) 
           ? textResults[id].Gem 
           : vectorResults[id].Gem;
       
       return (gem, relevance);
   })
   .OrderByDescending(r => r.relevance)
   .Take(topK)
   .ToList();
   ```
4. Normalize scores to 0-1 range before combining

---

### Phase 3 Detailed Tasks: Q&A Synthesis & Polish

#### TASK-045 & TASK-046: Category Synthesis Implementation

**Current State:** No synthesis logic  
**Target State:** Full category analysis and summary generation  
**Files Affected:** New SynthesisService.cs, CategorySynthesis MediatR handler

**Implementation Steps:**
1. Create SynthesisService interface:
   ```csharp
   public interface ISynthesisService
   {
       Task<CategorySynthesisResult> SynthesizeCategoryAsync(
           Guid categoryId,
           Guid tenantId,
           CancellationToken ct = default);
   }
   ```
2. Implement synthesis algorithm:
   ```csharp
   public async Task<CategorySynthesisResult> SynthesizeCategoryAsync(
       Guid categoryId,
       Guid tenantId,
       CancellationToken ct)
   {
       var cacheKey = $"synthesis:{tenantId}:{categoryId}";
       var cached = await _cache.GetAsync<CategorySynthesisResult>(cacheKey);
       if (cached != null) return cached;

       var gems = await _gemRepository.GetByCategoryAsync(categoryId, ct);
       if (!gems.Any()) 
           return new CategorySynthesisResult(categoryId, "No GEMs in this category yet.", []);

       var summaries = string.Join("\n\n", gems
           .Where(g => g.Summary != null)
           .Select(g => $"[{g.Title}]: {g.Summary.Text}"));

       var prompt = $@"
Synthesize the following documents into a comprehensive summary with key themes:

{summaries}

Format:
Summary: [2-3 paragraph overview]
Key Themes:
- Theme 1
- Theme 2
- Theme 3";

       var response = await _llmProvider.CompletionAsync(prompt, ct);
       var result = new CategorySynthesisResult(categoryId, response.Text, gems.Select(g => g.Id).ToList());

       await _cache.SetAsync(cacheKey, result, TimeSpan.FromHours(24));
       return result;
   }
   ```
3. Handle edge cases: empty category, very large categories (limit to 100 GEMs)
4. Cache results with 24h TTL

---

#### TASK-050 & TASK-051: Q&A Engine with RAG Pattern

**Current State:** No Q&A implementation  
**Target State:** Question answering with source citation  
**Files Affected:** New QuestionAnswerService.cs, QA MediatR handler

**Implementation Steps:**
1. Create QuestionAnswerService:
   ```csharp
   public interface IQuestionAnswerService
   {
       Task<QuestionAnswerResult> AskQuestionAsync(
           string question,
           Guid categoryId,
           Guid tenantId,
           CancellationToken ct = default);
   }
   ```
2. Implement RAG retrieval:
   ```csharp
   public async Task<QuestionAnswerResult> AskQuestionAsync(
       string question,
       Guid categoryId,
       Guid tenantId,
       CancellationToken ct)
   {
       // Generate question embedding
       var questionEmbedding = await _embeddingProvider.GenerateEmbeddingAsync(question, ct);

       // Retrieve top-5 most relevant GEMs
       var relevantGems = await _gemRepository.SearchBySemanticSimilarityAsync(
           questionEmbedding,
           topK: 5,
           maxDistance: 0.7f,
           categoryFilter: categoryId,
           ct);

       if (!relevantGems.Any())
           return new QuestionAnswerResult(
               "I don't have enough information in this category to answer that question.",
               []);

       // Build context from GEM excerpts
       var context = string.Join("\n\n", relevantGems.Select((r, i) => 
           $"[Source {i + 1} - {r.Gem.Title}]:\n{r.Gem.Summary?.Text ?? r.Gem.Title}"));

       var prompt = $@"
Answer the following question based ONLY on the provided sources. 
Include citations using [Source N] format.

Question: {question}

Sources:
{context}

Answer with citations:";

       var response = await _llmProvider.CompletionAsync(prompt, ct);

       var sources = relevantGems.Select(r => new SourceCitation(
           r.Gem.Id,
           r.Gem.Title,
           r.Gem.Summary?.Text ?? "",
           r.Distance
       )).ToList();

       return new QuestionAnswerResult(response.Text, sources);
   }
   ```
3. Implement source citation parsing and validation
4. Cache answers with 1h TTL

---

## 4. Alternatives

### Alternative Implementations Considered

| Alternative | Approach | Why Not Chosen |
|-------------|----------|-----------------|
| **ALT-001** | Use Milvus or Pinecone instead of pgvector | Adds infrastructure complexity, pgvector sufficient for scale, keeps data in PostgreSQL |
| **ALT-002** | Implement local embedding model (Sentence Transformers) instead of OpenAI | Trade-off: quality vs cost. Local models need GPU, cloud models more robust |
| **ALT-003** | Use Elasticsearch for search instead of PostgreSQL FTS | Over-engineered for current scale, adds operational complexity, PostgreSQL FTS sufficient |
| **ALT-004** | Implement caching in memory instead of Redis | In-process caching lost on restart, Redis enables distributed caching later |
| **ALT-005** | Store embeddings in separate cache-only table | Complexity: sync issues. Storing in GEM table is simpler, same performance |
| **ALT-006** | Use LangChain.NET for Q&A instead of manual RAG | LangChain adds abstraction overhead, manual implementation simpler for this use case |
| **ALT-007** | Implement streaming responses for Q&A | Adds complexity: SignalR setup. Simple request/response sufficient for MVP |
| **ALT-008** | Auto-generate vector indices on schema migration | Risk: index creation blocks table. Manual index creation with separate migration safer |

---

## 5. Dependencies

### Internal Dependencies

- **DEP-001**: ILLMProvider interface fully implemented (SemanticKernelProvider ready)
- **DEP-002**: IEmbeddingProvider interface defined (implementation needed)
- **DEP-003**: IVectorStore interface defined (implementation needed)
- **DEP-004**: IGEMRepository interface exists (methods need adding)
- **DEP-005**: Domain events (GEMSummarizationCompleted, etc.) already defined
- **DEP-006**: ActivityLog entity already exists for event tracking
- **DEP-007**: MediatR query/command handlers partially implemented

### External Dependencies

- **DEP-008**: OpenAI API (embeddings endpoint) - requires embeddings model configured
- **DEP-009**: PostgreSQL pgvector extension installed and enabled (version 0.7.0+)
- **DEP-010**: Npgsql pgvector support (EF Core extension packages)
- **DEP-011**: Redis for embedding caching (docker-compose already includes)
- **DEP-012**: MinIO (already in docker-compose)

### Library Updates Needed

- **DEP-013**: Npgsql.EntityFrameworkCore.PostgreSQL 8.0.0+ (pgvector support)
- **DEP-014**: Microsoft.SemanticKernel 1.70.0+ (already in use)
- **DEP-015**: Pgvector.EntityFrameworkCore 0.2.0+ package (if using specialized package)

---

## 6. Files

### Files to Create (New)

| File | Purpose | Type |
|------|---------|------|
| SynthesisService.cs | Category synthesis orchestration | Service |
| QuestionAnswerService.cs | Q&A engine with RAG | Service |
| SearchService.cs | Hybrid search orchestration | Service |
| OpenAIEmbeddingProvider.cs | OpenAI embeddings implementation | Provider |
| RedisEmbeddingCache.cs (enhance existing) | Redis-backed embedding cache | Cache |
| PgVectorStore.cs | pgvector operations | Service |
| SearchQuery.cs | MediatR search query | Query |
| SearchQueryHandler.cs | MediatR search handler | Handler |
| SynthesisQuery.cs | MediatR synthesis query | Query |
| QuestionAnswerQuery.cs | MediatR Q&A query | Query |

### Files to Modify (Existing)

| File | Changes | Impact |
|------|---------|--------|
| [GEM.cs](../../../src/InfoDumpManager.Domain/Entities/GEM.cs) | Add TitleEmbedding, SummaryEmbedding vectors | Domain model |
| [IGEMRepository.cs](../../../src/InfoDumpManager.Domain/Repositories/IGEMRepository.cs) | Add semantic, fulltext, hybrid search methods | Repository contract |
| [GEMRepository.cs](../../../src/InfoDumpManager.Infrastructure/Repositories/GEMRepository.cs) | Implement new search methods | Repository impl |
| [ApplicationDbContext.cs](../../../src/InfoDumpManager.Infrastructure/Data/ApplicationDbContext.cs) | Configure vector columns, add pgvector mapping | EF Core config |
| [SummarizationAgent.cs](../../../src/InfoDumpManager.Application/Agents/Implementations/SummarizationAgent.cs) | Fill in LLM call logic | Agent |
| [CategorizationAgent.cs](../../../src/InfoDumpManager.Application/Agents/Implementations/CategorizationAgent.cs) | Fill in categorization algorithm | Agent |
| [TaggingAgent.cs](../../../src/InfoDumpManager.Application/Agents/Implementations/TaggingAgent.cs) | Fill in tagging logic | Agent |
| [GEMsController.cs](../../../src/InfoDumpManager.WebAPI/Controllers/GEMsController.cs) | Add search, synthesis endpoints | API |
| [CategoriesController.cs](../../../src/InfoDumpManager.WebAPI/Controllers/CategoriesController.cs) | Add synthesis, question endpoints | API |
| [Program.cs](../../../src/InfoDumpManager.WebAPI/Program.cs) | Register new services in DI | Configuration |

### Migration Files

| Migration | Changes | Order |
|-----------|---------|-------|
| `[timestamp]_AddVectorColumnsToGEM.cs` | Add title_embedding, summary_embedding | First |
| `[timestamp]_CreateVectorIndexesOnGEM.cs` | Create HNSW indexes | Second |
| `[timestamp]_AddEmbeddingMetadataColumns.cs` | Add model_name, token_count, cache_hit columns | Third |

---

## 7. Testing

### Unit Tests to Add

| Test Class | Test Cases | Purpose |
|-----------|-----------|---------|
| SummarizationAgentTests | 5+ | Test LLM integration, error handling, event publishing |
| CategorizationAgentTests | 7+ | Test category analysis, confidence scoring, auto-assignment |
| TaggingAgentTests | 6+ | Test embedding generation, tag suggestion, caching |
| SearchServiceTests | 8+ | Test fulltext, semantic, hybrid search, ranking |
| SynthesisServiceTests | 4+ | Test category synthesis, caching, error handling |
| QuestionAnswerServiceTests | 5+ | Test RAG retrieval, answer generation, citations |
| EmbeddingProviderTests | 4+ | Test embedding generation, tokenization, API errors |
| EmbeddingCacheTests | 5+ | Test cache hit/miss, expiration, Redis operations |

### Integration Tests to Add

| Test Class | Test Cases | Purpose |
|-----------|-----------|---------|
| VectorSearchIntegrationTests | 6+ | Test pgvector queries with real database |
| SemanticSearchIntegrationTests | 5+ | Test similarity scoring with synthetic embeddings |
| SearchApiIntegrationTests | 8+ | Test search endpoints with various filters |
| SynthesisApiIntegrationTests | 4+ | Test synthesis endpoints, caching |
| QuestionAnswerApiIntegrationTests | 5+ | Test Q&A endpoints, source citation format |
| EndToEndWorkflowTests | 3+ | Test complete flow: ingest → summarize → search |

### Test Data & Fixtures

- **FIXTURE-001**: Sample GEMs with pre-computed embeddings
- **FIXTURE-002**: Category test data with 20+ GEMs per category
- **FIXTURE-003**: Tag test data with similarity relationships
- **FIXTURE-004**: Search queries with expected result sets for validation

### Coverage Requirements

- **Target**: 85% code coverage for new services
- **Critical paths**: 100% for agent execution, search ranking, Q&A generation
- **Excluded**: EF Core query projections (covered by integration tests)

---

## 8. Risks & Assumptions

### Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|-----------|
| **RISK-001**: OpenAI embedding API rate limits causing search latency | Medium | High | Cache embeddings aggressively, implement local fallback model |
| **RISK-002**: pgvector index creation blocks production table | Low | High | Create index in maintenance window, monitor creation time |
| **RISK-003**: Vector embedding quality insufficient for good search results | Medium | High | Validate with sample queries, iterate on prompt engineering |
| **RISK-004**: Category synthesis LLM calls expensive (token costs) | Medium | Medium | Implement synthesis caching, limit category size, batch processing |
| **RISK-005**: Q&A responses hallucinate (inaccurate citations) | Medium | High | Implement citation validation, limit context to top-5 GEMs |
| **RISK-006**: Search latency over 500ms with 10,000+ GEMs | Low | Medium | Optimize query plans, implement result set pagination, denormalize columns |
| **RISK-007**: Team unfamiliar with pgvector EF Core extension | Medium | Medium | Invest in training, reference documentation, pair programming |

### Assumptions

| Assumption | Risk Level | Mitigation |
|-----------|------------|-----------|
| **ASS-001**: OpenAI embeddings API available and reliable | Medium | Have fallback to local Sentence Transformers model |
| **ASS-002**: PostgreSQL pgvector extension installed in production | Medium | Verify in environment setup, document requirements |
| **ASS-003**: Vector similarity provides useful search results | Medium | Plan user testing phase to validate ranking quality |
| **ASS-004**: LLM summarization quality acceptable without manual review | Medium | Implement quality metrics, confidence thresholds, user feedback loop |
| **ASS-005**: Team can deliver 2-3 weeks of focused development | Low | Ensure dedicated capacity, minimize distractions |
| **ASS-006**: No breaking changes in dependencies during implementation | Low | Pin versions, weekly dependency updates check |

---

## 9. Related Specifications / Further Reading

### Architecture Documents
- [Epic Architecture Specification](arch.md) - Overall system design
- [Phase 1 Implementation Plan](implementation-plan-1.md) - Foundation setup
- [Architecture Review Report](../../ImplementationProcessReports/arch.md_implementation-review-2026-02-04.md) - Gaps analysis

### Technical References
- [PostgreSQL pgvector Documentation](https://github.com/pgvector/pgvector) - Vector operations
- [Entity Framework Core Docs](https://learn.microsoft.com/en-us/ef/core/) - ORM patterns
- [Microsoft.SemanticKernel Guide](https://learn.microsoft.com/en-us/semantic-kernel/) - LLM orchestration
- [OpenAI Embeddings API](https://platform.openai.com/docs/guides/embeddings) - Embedding generation

### Code References
- [ILLMProvider Interface](../../../src/InfoDumpManager.Application/Services/LLM/ILLMProvider.cs)
- [IEmbeddingProvider Interface](../../../src/InfoDumpManager.Application/Services/Embeddings/IEmbeddingProvider.cs)
- [IVectorStore Interface](../../../src/InfoDumpManager.Application/Services/Embeddings/IVectorStore.cs)
- [Domain Events](../../../src/InfoDumpManager.Domain/Events/GEMProcessingEvents.cs)

---

## 10. Success Criteria & Acceptance

### Feature Completion Checklist

- [ ] All 7 partially implemented features achieve 100% completion
- [ ] All feature requirements (REQ-001 through REQ-010) satisfied
- [ ] All non-functional requirements (NFR-001 through NFR-007) validated via benchmarks
- [ ] All 78 tasks completed and tested
- [ ] 85%+ code coverage achieved
- [ ] Zero critical security issues in audit
- [ ] Performance benchmarks meet or exceed targets

### Quality Gates

- [ ] All unit tests passing (100+/100+)
- [ ] All integration tests passing (40+/40+)
- [ ] Code review approved by 2+ senior developers
- [ ] Load testing validates 1000+ GEM search performance
- [ ] Accessibility validation (WCAG AA)
- [ ] Security audit passed

### Definition of Done

Each task is "done" when:
1. Code implementation complete and compiles without errors
2. Unit tests written and passing (>90% coverage for new code)
3. Integration tests written and passing
4. Code reviewed and approved
5. Documentation written (comments, README updates)
6. Commits pushed and PR merged to main branch

---

## Execution Timeline

### Recommended Schedule

**Week 1 (Phase 1):**
- Mon-Tue: AI agents completion (Tasks 001-020)
- Wed-Thu: Write agent tests
- Fri: Code review and refactoring

**Week 2 (Phase 2):**
- Mon-Tue: Vector DB integration (Tasks 021-044)
- Wed: Write search tests
- Thu: Search performance optimization
- Fri: Code review

**Week 3 (Phase 3):**
- Mon: Q&A synthesis (Tasks 045-058)
- Tue-Wed: MinIO completion + UI components (Tasks 059-069)
- Thu: Testing and documentation (Tasks 070-078)
- Fri: Final review and deployment prep

**Resource Allocation:**
- 2-3 developers working in parallel
- 1 QA engineer for testing/validation
- 1 technical lead for architecture guidance

---

**Status:** Ready for execution  
**Review Date:** 2026-02-04  
**Next Review:** Upon completion or 2026-02-18 checkpoint
