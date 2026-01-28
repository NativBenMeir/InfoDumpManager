---
goal: Implementation Plan for GEM Ingestion, Summarization, and Smart Categorization System
phase_title: Auto-Categorization, Tagging & Search Infrastructure
PhaseNumber: 9
version: 1.1
date_created: 2026-01-28
last_updated: 2026-01-28
tags: [categorization, tagging, search, ai, pgvector]
depends_on: [1, 2, 3, 4, 5, 7, 8]
status: Planned
status_color: blue
---

# Introduction

![Status: Planned](https://img.shields.io/badge/Status-Planned-blue)

This phase implements AI-powered automatic categorization and semantic tagging, along with the search infrastructure. It extends the database schema with Tag entities and pgvector columns, implements embedding generation, creates background services for categorization and tagging, and builds the search service supporting full-text, semantic, and hybrid search modes.

## 1. Requirements & Constraints

- **REQ-003**: System must support automatic categorization using AI analysis of content and existing category structure
- **REQ-004**: System must generate and apply semantic tags for both intra-category and cross-category linking
- **REQ-006**: System must provide manual tag management (create, rename, delete, apply, remove)
- **REQ-007**: System must support full-text and semantic search across GEMs
- **CON-001**: Must use .NET 10.0.2 LTS as primary framework
- **CON-002**: Must use PostgreSQL 16.11 with pgvector extension for data persistence
- **CON-004**: Must follow domain-driven design with clear layer separation
- **CON-005**: Must support both self-hosted (Docker Compose) and future SaaS (K8s-ready) deployment
- **NFR-002**: System must be designed for multi-tenant SaaS scalability from day one
- **NFR-003**: All data must be encrypted at rest and in transit
- **NFR-004**: System must provide comprehensive observability (logging, metrics, tracing)
- **SEC-003**: Implement claims-based authorization with multi-tenancy support
- **SEC-004**: Ensure row-level security for multi-tenant data isolation
- **GUD-001**: Write unit tests for all domain logic and application services
- **GUD-002**: Write integration tests using Testcontainers 4.10.0 for data access and API layers
- **GUD-003**: Use MediatR 14.0.0 for CQRS pattern implementation
- **GUD-004**: Use FluentValidation 12.1.1 for all input validation
- **GUD-005**: Use Serilog 4.3.0 with structured logging throughout
- **GUD-006**: Generate OpenAPI specs and strongly-typed clients for all APIs
- **GUD-007**: Follow Repository and Unit of Work patterns for data access
- **GUD-008**: Implement circuit breaker and retry policies with Polly 8.6.5
- **GUD-009**: Use AutoMapper 16.0.0 for entity-to-DTO mappings
- **GUD-010**: Maintain comprehensive API documentation with examples
- **PAT-001**: Domain-Driven Design with Aggregates, Entities, and Value Objects
- **PAT-002**: CQRS-lite pattern for read/write separation where appropriate
- **PAT-003**: Event-driven background processing for async operations
- **PAT-004**: Repository pattern with Unit of Work for data access abstraction
- **PAT-005**: Strategy pattern for LLM provider abstraction
- **PAT-006**: Factory pattern for creating domain entities with validation
- **PAT-007**: Specification pattern for complex query logic

## 2. Implementation Steps

### Implementation

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-036 | Design and implement prompt templates for categorization (existing categories + GEM content → suggest category) | | |
| TASK-041 | Implement AI Categorization Background Service that processes summarized GEMs for category assignment | | |
| TASK-043 | Implement categorization logic: analyze content + fetch existing categories → call LLM → parse response (existing category ID or new category name) | | |
| TASK-044 | Add database columns for AI metadata: summary_model, summary_tokens, category_confidence, category_suggested_by_ai | | |
| TASK-045 | Create EF Core migration for AI metadata columns and update DbContext | | |
| TASK-053 | Implement categorization confidence threshold (e.g., only auto-assign if confidence > 0.7, otherwise flag for manual review) | | |
| TASK-054 | Add activity log entries for AI operations: SummarizationCompleted, CategorizationSuggested, CategorizationAccepted | | |
| TASK-055 | Implement Redis caching for frequently accessed categories to reduce database queries during categorization | | |
| TASK-057 | Write integration tests for summarization and categorization workflows using test LLM provider or mocked responses | | |

## 3. Alternatives

- **ALT-002**: Separate Vector Database (Qdrant, Pinecone) Instead of pgvector - Rejected to minimize infrastructure complexity
- **ALT-006**: Local LLM (Ollama, LM Studio) as Primary Provider - Kept as alternative for privacy-focused deployment

## 4. Dependencies

- **PHASE-DEP-011**: Requires summarization from Phase 8 - Verify summaries are being generated
- **PHASE-DEP-012**: Requires LLM providers from Phase 7 - Verify embedding generation capability
- **DEP-001**: LLM API Provider (OpenAI or Azure OpenAI) - Required for categorization and embeddings
- **DEP-002**: Embedding API Provider - Required for semantic search
- **DEP-004**: Redis - Required for distributed caching
- **DEP-009**: Microsoft.SemanticKernel 1.70.0 - For prompt management

## 5. Files

- **FILE-042**: `src/InfoDumpManager.Infrastructure/BackgroundServices/CategorizationBackgroundService.cs` - Categorization worker
- **FILE-042-P9**: `src/InfoDumpManager.Infrastructure/Services/EmbeddingService.cs` - Embedding generation service
- **FILE-042-P9**: `src/InfoDumpManager.Infrastructure/Services/CategorizationService.cs` - Categorization logic
- **FILE-039-P9**: `src/InfoDumpManager.Infrastructure/Prompts/CategorizationPrompt.txt` - Categorization prompt template
- **FILE-032-P9**: `src/InfoDumpManager.Infrastructure/Data/Configurations/GEMConfigurationExtended.cs` - Updated GEM configuration with AI metadata
- **FILE-033-P9**: `src/InfoDumpManager.Infrastructure/Migrations/AddAIMetadataColumns.cs` - Migration for AI metadata

## 6. Testing

- **TEST-064**: Unit Test - Categorization Prompt - Format with categories list - Expected: Proper prompt structure
- **TEST-065**: Integration Test - Categorization Service - Suggest category for sample GEM - Expected: Valid category suggestion
- **TEST-066**: Integration Test - Categorization - Confidence threshold - Expected: Low confidence suggestions flagged for review
- **TEST-067**: Integration Test - Redis Cache - Category caching - Expected: Cache hit on second access
- **TEST-068**: Integration Test - Activity Log - AI operations logged - Expected: All AI events in database
- **TEST-069**: Integration Test - End-to-End - GEM creation → summarization → categorization - Expected: Complete workflow
- **TEST-070**: Unit Test - Mock LLM - Categorization with mock response - Expected: Deterministic category assignment
- **TEST-071**: Performance Test - Categorization - Process 100 GEMs - Expected: Average latency < 5 seconds per GEM

### Test Requirements
- Categorization workflow must be tested end-to-end
- Confidence threshold logic must be validated
- Redis caching must improve performance measurably
- All AI operations must be logged to activity log

## 7. Risks & Assumptions

- **RISK-020**: Categorization accuracy depends on prompt quality - Mitigation: Iterate on prompts with feedback
- **RISK-021**: Existing category list may become too long for prompt context - Mitigation: Implement category embedding similarity for pre-filtering
- **RISK-022**: Redis cache invalidation strategy needed for category updates - Mitigation: Implement cache eviction on category modifications
- **ASSUMPTION-018**: Categorization confidence scores are reliable indicators of accuracy
- **ASSUMPTION-019**: Most GEMs will match existing categories rather than requiring new ones

## 8. Success Metrics

- **METRIC-002**: All TEST-XXX tests passing (exit code 0)
- **METRIC-003**: Build successful with no errors (exit code 0)
- **METRIC-034**: Categorization accuracy > 75% on manual validation of 50 samples
- **METRIC-035**: Redis cache hit rate > 80% for category queries during categorization
- **METRIC-036**: End-to-end workflow (ingest → summarize → categorize) completes in < 30 seconds (p95)
- **METRIC-037**: All AI metadata columns populated correctly for categorized GEMs

## 9. Related Specifications / Further Reading

- [pgvector Extension Documentation](https://github.com/pgvector/pgvector)
- [Semantic Search Best Practices](https://www.pinecone.io/learn/semantic-search/)
- [Redis Caching Patterns](https://redis.io/docs/manual/patterns/)
- [Prompt Engineering for Classification](https://platform.openai.com/docs/guides/prompt-engineering)
