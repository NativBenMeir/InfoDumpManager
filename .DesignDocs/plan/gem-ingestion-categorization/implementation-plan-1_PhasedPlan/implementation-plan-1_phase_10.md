---
goal: Implementation Plan for GEM Ingestion, Summarization, and Smart Categorization System
phase_title: Tagging, Semantic Search & Q&A Implementation
PhaseNumber: 10
version: 1.1
date_created: 2026-01-28
last_updated: 2026-01-28
tags: [tagging, search, qa, rag, embeddings, semantic-search]
depends_on: [1, 2, 3, 4, 5, 7, 8, 9]
status: Planned
status_color: blue
---

# Introduction

![Status: Planned](https://img.shields.io/badge/Status-Planned-blue)

This phase completes the AI-powered features by implementing semantic tagging, vector-based search with pgvector, and Q&A synthesis using RAG (Retrieval-Augmented Generation) patterns. It extends the schema with Tag entities, implements embedding generation for semantic search, creates comprehensive search capabilities, and delivers category-level synthesis and question answering with source citations.

## 1. Requirements & Constraints

- **REQ-004**: System must generate and apply semantic tags for both intra-category and cross-category linking
- **REQ-006**: System must provide manual tag management (create, rename, delete, apply, remove)
- **REQ-007**: System must support full-text and semantic search across GEMs
- **REQ-008**: System must provide on-demand category-level synthesis and Q&A
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
| TASK-061 | Design and implement Tag entity in Domain layer with fields: tag_id, name, description, created_by, created_at | | |
| TASK-062 | Design and implement GEMTag join entity for many-to-many relationship between GEMs and Tags | | |
| TASK-063 | Add embedding vector columns to GEM table (pgvector type) for semantic search: title_embedding, summary_embedding | | |
| TASK-064 | Create EF Core migration for Tag, GEMTag tables and pgvector columns | | |
| TASK-065 | Configure pgvector extension in DbContext with proper index strategy (HNSW or IVFFlat for vector similarity) | | |
| TASK-066 | Implement embedding generation service that calls LLM provider for text → vector conversion | | |
| TASK-067 | Implement background service to generate embeddings for existing GEMs (backfill job) | | |
| TASK-068 | Modify CreateGEMCommand handler to generate embeddings after summarization completes | | |

## 3. Alternatives

- **ALT-002**: Separate Vector Database (Qdrant, Pinecone) Instead of pgvector - Rejected to minimize infrastructure complexity and maintain data locality

## 4. Dependencies

- **PHASE-DEP-013**: Requires categorization from Phase 9 - Verify categorization is functional
- **PHASE-DEP-014**: Requires LLM embedding capability from Phase 7 - Verify embedding generation works
- **DEP-001**: LLM API Provider (OpenAI or Azure OpenAI) - Required for embeddings and Q&A
- **DEP-002**: Embedding API Provider - Required for semantic search
- **DEP-003**: PostgreSQL 16.11 with pgvector Extension - Required for vector similarity search

## 5. Files

- **FILE-014**: `src/InfoDumpManager.Domain/Entities/Tag.cs` - Tag entity
- **FILE-014-P10**: `src/InfoDumpManager.Domain/Entities/GEMTag.cs` - GEMTag join entity
- **FILE-019**: `src/InfoDumpManager.Domain/Repositories/ITagRepository.cs` - Tag repository interface
- **FILE-035-P10**: `src/InfoDumpManager.Infrastructure/Repositories/TagRepository.cs` - Tag repository implementation
- **FILE-046**: `src/InfoDumpManager.WebAPI/Controllers/TagsController.cs` - Tag API endpoints
- **FILE-047**: `src/InfoDumpManager.WebAPI/Controllers/SearchController.cs` - Search API endpoints
- **FILE-048**: `src/InfoDumpManager.WebAPI/Controllers/QueryController.cs` - Q&A and synthesis endpoints
- **FILE-043**: `src/InfoDumpManager.Infrastructure/BackgroundServices/TaggingBackgroundService.cs` - Tagging worker
- **FILE-043-P10**: `src/InfoDumpManager.Infrastructure/Services/SearchService.cs` - Search implementation
- **FILE-043-P10**: `src/InfoDumpManager.Infrastructure/Services/QueryService.cs` - Q&A RAG implementation
- **FILE-057**: `src/InfoDumpManager.Web/Pages/Tags/Manage.cshtml` - Tag management page
- **FILE-058**: `src/InfoDumpManager.Web/Pages/Search/Index.cshtml` - Search page
- **FILE-059**: `src/InfoDumpManager.Web/Pages/Categories/View.cshtml` - Category view with Q&A interface

## 6. Testing

- **TEST-072**: Integration Test - Tag Management - Create and apply tags - Expected: Tags saved and associated with GEMs
- **TEST-073**: Integration Test - Embedding Generation - Generate embeddings for text - Expected: Valid vector returned
- **TEST-074**: Integration Test - Vector Search - Semantic similarity query - Expected: Relevant GEMs returned by similarity
- **TEST-075**: Integration Test - Search Service - Full-text search - Expected: Matches based on text content
- **TEST-076**: Integration Test - Search Service - Hybrid search - Expected: Combined results from text and semantic
- **TEST-077**: Integration Test - Q&A Service - Ask question about category - Expected: Answer with source citations
- **TEST-078**: Integration Test - Category Synthesis - Generate category summary - Expected: Comprehensive synthesis
- **TEST-079**: Performance Test - Vector Search - Query latency - Expected: < 500ms for p95
- **TEST-080**: Integration Test - Tag Suggestions - AI suggests tags - Expected: Relevant tags for GEM content

### Test Requirements
- Vector search must be tested with real pgvector queries
- Search ranking algorithm must be validated for relevance
- Q&A answers must include proper source citations
- Tag suggestion quality must be manually validated

## 7. Risks & Assumptions

- **RISK-023**: Vector search performance may degrade with large datasets - Mitigation: Use HNSW indexing for scalability
- **RISK-024**: Q&A answer quality depends on retrieval accuracy - Mitigation: Tune similarity thresholds and top-k parameters
- **RISK-025**: Tag suggestion quality varies with content type - Mitigation: Iterate on prompt engineering
- **ASSUMPTION-020**: HNSW index provides acceptable performance up to 100K vectors
- **ASSUMPTION-021**: Top-k retrieval (k=5-10) provides sufficient context for Q&A

## 8. Success Metrics

- **METRIC-002**: All TEST-XXX tests passing (exit code 0)
- **METRIC-003**: Build successful with no errors (exit code 0)
- **METRIC-038**: Vector search p95 latency < 500ms
- **METRIC-039**: Search relevance: top 5 results include expected match in 80% of test queries
- **METRIC-040**: Q&A answers include correct source citations in 90% of test cases
- **METRIC-041**: Tag suggestion relevance rated as good/excellent in 70% of manual reviews
- **METRIC-042**: Hybrid search outperforms individual modes by 15% in relevance metrics

## 9. Related Specifications / Further Reading

- [pgvector Documentation](https://github.com/pgvector/pgvector)
- [HNSW Index Performance](https://github.com/pgvector/pgvector#hnsw)
- [RAG Pattern Best Practices](https://www.anthropic.com/index/retrieval-augmented-generation-rag)
- [Semantic Search Implementation Guide](https://www.pinecone.io/learn/semantic-search/)
