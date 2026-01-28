---
goal: Implementation Plan for GEM Ingestion, Summarization, and Smart Categorization System
version: 1.0
date_created: 2026-01-23
last_updated: 2026-01-23
owner: Development Team
status: 'Planned'
tags: [feature, architecture, ai, epic, gem-system]
---

# Introduction

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

This implementation plan provides a detailed, phase-by-phase breakdown for building the GEM (Generated Enriched Memory) Ingestion, Summarization, and Smart Categorization system. The plan is based on the Epic Architecture Specification and follows a 4-phase delivery approach spanning 13-17 weeks. Each phase builds incrementally on the previous one, delivering working software with measurable value at each milestone.

The system will enable users to capture web content, automatically generate AI-powered summaries, intelligently categorize information, apply semantic tags, and query their knowledge base through natural language Q&A. The architecture follows domain-driven design principles with ASP.NET Core, PostgreSQL with pgvector, and containerized deployment via Docker.

## 1. Requirements & Constraints

### Functional Requirements
- **REQ-001**: System must ingest web pages via URL submission with headless browser rendering
- **REQ-002**: System must generate AI-powered summaries for all ingested content
- **REQ-003**: System must support automatic categorization using AI analysis of content and existing category structure
- **REQ-004**: System must generate and apply semantic tags for both intra-category and cross-category linking
- **REQ-005**: System must provide manual category management (create, rename, merge, delete, reassign)
- **REQ-006**: System must provide manual tag management (create, rename, delete, apply, remove)
- **REQ-007**: System must support full-text and semantic search across GEMs
- **REQ-008**: System must provide on-demand category-level synthesis and Q&A
- **REQ-009**: System must maintain activity logs for all GEM operations and AI actions
- **REQ-010**: System must store original web page snapshots with source links

### Non-Functional Requirements
- **NFR-001**: Ingestion + summarization must complete in < 15 seconds (p95) for typical web pages
- **NFR-002**: System must be designed for multi-tenant SaaS scalability from day one
- **NFR-003**: All data must be encrypted at rest and in transit
- **NFR-004**: System must provide comprehensive observability (logging, metrics, tracing)
- **NFR-005**: Web UI must meet WCAG AA accessibility standards
- **NFR-006**: System must handle tens of GEMs per user per day
- **NFR-007**: All services must be containerized via Docker

### Security Requirements
- **SEC-001**: Implement ASP.NET Core Identity for authentication and user management
- **SEC-002**: Use JWT bearer tokens for API authentication
- **SEC-003**: Implement claims-based authorization with multi-tenancy support
- **SEC-004**: Ensure row-level security for multi-tenant data isolation
- **SEC-005**: Store all secrets in environment variables or secure vaults (not in code)

### Architectural Constraints
- **CON-001**: Must use .NET 8.0 LTS as primary framework
- **CON-002**: Must use PostgreSQL 16 with pgvector extension for data persistence
- **CON-003**: Must use ASP.NET Core for all web applications and APIs
- **CON-004**: Must follow domain-driven design with clear layer separation
- **CON-005**: Must support both self-hosted (Docker Compose) and future SaaS (K8s-ready) deployment
- **CON-006**: Must use Entity Framework Core for Phase 1-3; Dapper optional for Phase 4 optimization
- **CON-007**: All background processing must use IHostedService/BackgroundService patterns
- **CON-008**: Must abstract LLM provider to support OpenAI, Azure OpenAI, and local models

### Development Guidelines
- **GUD-001**: Write unit tests for all domain logic and application services
- **GUD-002**: Write integration tests using Testcontainers for data access and API layers
- **GUD-003**: Use MediatR for CQRS pattern implementation
- **GUD-004**: Use FluentValidation for all input validation
- **GUD-005**: Use Serilog with structured logging throughout
- **GUD-006**: Generate OpenAPI specs and strongly-typed clients for all APIs
- **GUD-007**: Follow Repository and Unit of Work patterns for data access
- **GUD-008**: Implement circuit breaker and retry policies with Polly
- **GUD-009**: Use AutoMapper for entity-to-DTO mappings
- **GUD-010**: Maintain comprehensive API documentation with examples

### Design Patterns
- **PAT-001**: Domain-Driven Design with Aggregates, Entities, and Value Objects
- **PAT-002**: CQRS-lite pattern for read/write separation where appropriate
- **PAT-003**: Event-driven background processing for async operations
- **PAT-004**: Repository pattern with Unit of Work for data access abstraction
- **PAT-005**: Strategy pattern for LLM provider abstraction
- **PAT-006**: Factory pattern for creating domain entities with validation
- **PAT-007**: Specification pattern for complex query logic

## 2. Implementation Steps

### Implementation Phase 1: Foundation & Basic Ingestion (4-5 weeks)

**GOAL-001**: Establish foundational architecture with domain model, database schema, basic web ingestion, and manual categorization capabilities

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Set up .NET 8 solution structure with projects: Domain, Application, Infrastructure, WebAPI, Web, Tests.Unit, Tests.Integration | | |
| TASK-002 | Configure Docker Compose with PostgreSQL 16 + pgvector extension, Redis, MinIO, and development nginx reverse proxy | | |
| TASK-003 | Design and implement GEM Aggregate in Domain layer with entities: GEM, GEMSource (value object), GEMSnapshot (value object), GEMSummary (value object) | | |
| TASK-004 | Design and implement Category Aggregate with Category entity and GEM assignments | | |
| TASK-005 | Design and implement User entity with ASP.NET Core Identity integration | | |
| TASK-006 | Design and implement ActivityLog entity for audit trail with event types (GEMCreated, GEMUpdated, CategoryAssigned, etc.) | | |
| TASK-007 | Create PostgreSQL schema with EF Core migrations for GEM, Category, User, ActivityLog tables with proper indexes | | |
| TASK-008 | Implement Repository interfaces in Domain layer (IGEMRepository, ICategoryRepository, IActivityLogRepository) | | |
| TASK-009 | Implement concrete repositories in Infrastructure layer using EF Core DbContext | | |
| TASK-010 | Implement Unit of Work pattern in Infrastructure layer | | |
| TASK-011 | Set up ASP.NET Core Identity with user registration and login endpoints | | |
| TASK-012 | Implement JWT bearer token authentication for API access | | |
| TASK-013 | Create GEM API Controller with endpoints: POST /api/v1/gems (create), GET /api/v1/gems/{id}, GET /api/v1/gems (list with pagination) | | |
| TASK-014 | Create Category API Controller with endpoints: POST /api/v1/categories, GET /api/v1/categories, PUT /api/v1/categories/{id}, DELETE /api/v1/categories/{id} | | |
| TASK-015 | Implement Web Scraping Service using Playwright with URL validation, content fetching, and HTML cleaning | | |
| TASK-016 | Implement MinIO integration for storing web page snapshots (HTML) in object storage | | |
| TASK-017 | Create MediatR command handlers: CreateGEMCommand, AssignCategoryCommand, CreateCategoryCommand | | |
| TASK-018 | Implement FluentValidation validators for all commands and DTOs | | |
| TASK-019 | Create basic ASP.NET Core Razor Pages Web UI for GEM submission (URL input form) | | |
| TASK-020 | Create Web UI pages for GEM list view with pagination and basic filtering by category | | |
| TASK-021 | Create Web UI pages for GEM detail view showing title, source link, snapshot preview, and category assignment | | |
| TASK-022 | Create Web UI pages for category management (list, create, edit, delete with confirmation) | | |
| TASK-023 | Implement manual category assignment UI in GEM detail page with dropdown selector | | |
| TASK-024 | Set up Serilog with console and file sinks for development logging | | |
| TASK-025 | Configure Swagger/NSwag for API documentation and client generation | | |
| TASK-026 | Write unit tests for domain entities, value objects, and validation logic (target: 80% coverage) | | |
| TASK-027 | Write integration tests for GEM and Category API endpoints using Testcontainers for PostgreSQL | | |
| TASK-028 | Implement basic error handling middleware with structured error responses | | |
| TASK-029 | Create initial database seed data with sample categories for development | | |
| TASK-030 | Document Phase 1 API endpoints with examples in README or docs/api.md | | |

### Implementation Phase 2: AI Summarization & Auto-Categorization (4-5 weeks)

**GOAL-002**: Integrate LLM providers, implement background job processing, and deliver AI-powered summarization and automatic categorization features

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-031 | Implement LLM Provider abstraction layer (ILLMProvider interface) with methods for completion, embedding generation | | |
| TASK-032 | Implement OpenAI provider using Azure.AI.OpenAI or OpenAI SDK with configuration for API keys, model selection, temperature, max tokens | | |
| TASK-033 | Implement Azure OpenAI provider as alternative implementation of ILLMProvider | | |
| TASK-034 | Create LLM Orchestration Service using Semantic Kernel or LangChain.NET for prompt management and chaining | | |
| TASK-035 | Design and implement prompt templates for summarization (system prompt + user content template) with version tracking | | |
| TASK-036 | Design and implement prompt templates for categorization (existing categories + GEM content → suggest category) | | |
| TASK-037 | Implement token counting and cost tracking service to monitor LLM API usage | | |
| TASK-038 | Implement Polly retry policies and circuit breaker for LLM API calls with exponential backoff | | |
| TASK-039 | Set up background job queue infrastructure using System.Threading.Channels for producer-consumer pattern | | |
| TASK-040 | Implement AI Summarization Background Service (inherits BackgroundService) that processes GEMs from queue | | |
| TASK-041 | Implement AI Categorization Background Service that processes summarized GEMs for category assignment | | |
| TASK-042 | Add GEMSummary generation to summarization service with fields: summary text, generated timestamp, model used, token count | | |
| TASK-043 | Implement categorization logic: analyze content + fetch existing categories → call LLM → parse response (existing category ID or new category name) | | |
| TASK-044 | Add database columns for AI metadata: summary_model, summary_tokens, category_confidence, category_suggested_by_ai | | |
| TASK-045 | Create EF Core migration for AI metadata columns and update DbContext | | |
| TASK-046 | Implement job status tracking entity (JobStatus table) with fields: job_id, job_type, status, created_at, completed_at, error_message | | |
| TASK-047 | Modify CreateGEMCommand handler to enqueue summarization job after saving GEM | | |
| TASK-048 | Implement webhook or polling mechanism to notify Web UI when summarization completes (SignalR or simple polling) | | |
| TASK-049 | Update GEM detail page to show AI-generated summary with "Regenerate Summary" button | | |
| TASK-050 | Update GEM detail page to show AI-suggested category with "Accept" or "Change" actions | | |
| TASK-051 | Create admin page for managing LLM provider settings (API keys, model selection, temperature) via appsettings or database | | |
| TASK-052 | Implement summary regeneration endpoint: POST /api/v1/gems/{id}/regenerate-summary | | |
| TASK-053 | Implement categorization confidence threshold (e.g., only auto-assign if confidence > 0.7, otherwise flag for manual review) | | |
| TASK-054 | Add activity log entries for AI operations: SummarizationCompleted, CategorizationSuggested, CategorizationAccepted | | |
| TASK-055 | Implement Redis caching for frequently accessed categories to reduce database queries during categorization | | |
| TASK-056 | Write unit tests for LLM provider abstraction and mock LLM responses for deterministic testing | | |
| TASK-057 | Write integration tests for summarization and categorization workflows using test LLM provider or mocked responses | | |
| TASK-058 | Implement metrics collection for summarization latency, categorization accuracy, LLM token usage using prometheus-net | | |
| TASK-059 | Document prompt engineering decisions and version history in docs/prompts/ directory | | |
| TASK-060 | Perform manual testing and prompt tuning to achieve acceptable summarization quality and categorization accuracy | | |

### Implementation Phase 3: Tagging, Search & Q&A Synthesis (3-4 weeks)

**GOAL-003**: Implement semantic tagging, vector-based search, and category-level Q&A synthesis capabilities

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
| TASK-069 | Design prompt template for tag suggestion: analyze GEM content + existing tags → suggest 3-5 relevant tags | | |
| TASK-070 | Implement AI Tagging Background Service that suggests tags after categorization completes | | |
| TASK-071 | Create Tag API Controller with endpoints: GET /api/v1/tags, POST /api/v1/tags, PUT /api/v1/tags/{id}, DELETE /api/v1/tags/{id} | | |
| TASK-072 | Create Tag API endpoints for applying/removing tags: POST /api/v1/gems/{id}/tags, DELETE /api/v1/gems/{id}/tags/{tagId} | | |
| TASK-073 | Implement tag management UI pages: tag list, create tag, rename tag, delete tag with confirmation | | |
| TASK-074 | Update GEM detail page to show AI-suggested tags with "Accept All", "Accept Selected", or "Ignore" actions | | |
| TASK-075 | Implement manual tag application UI in GEM detail page with autocomplete search for existing tags | | |
| TASK-076 | Implement tag filtering in GEM list view with multi-select tag filter UI | | |
| TASK-077 | Implement search service with three search modes: full-text (PostgreSQL FTS), semantic (pgvector similarity), hybrid (combined) | | |
| TASK-078 | Create Search API endpoint: GET /api/v1/search?q={query}&mode=hybrid&category={id}&tags={tag1,tag2}&from={date}&to={date} | | |
| TASK-079 | Implement search results ranking algorithm that combines text relevance and vector similarity scores | | |
| TASK-080 | Create search UI page with search bar, filter options, and results list with relevance scoring | | |
| TASK-081 | Design prompt template for category synthesis: analyze all GEMs in category → generate comprehensive summary | | |
| TASK-082 | Design prompt template for Q&A: user question + category GEMs → answer with source citations | | |
| TASK-083 | Implement Query Service that orchestrates RAG (Retrieval-Augmented Generation) pattern: retrieve relevant GEMs → generate answer | | |
| TASK-084 | Create Query API endpoints: POST /api/v1/categories/{id}/synthesize, POST /api/v1/categories/{id}/ask | | |
| TASK-085 | Implement GEM retrieval strategy for Q&A: use semantic search to find top-k most relevant GEMs for question | | |
| TASK-086 | Implement answer generation with source citation: format response with GEM references and links | | |
| TASK-087 | Create category view UI page with "Generate Summary" button and Q&A chat interface | | |
| TASK-088 | Implement feedback mechanism for Q&A answers: thumbs up/down with optional comment stored in database | | |
| TASK-089 | Add Redis caching for category synthesis results with TTL to reduce redundant LLM calls | | |
| TASK-090 | Implement activity logging for search queries, synthesis requests, Q&A interactions | | |
| TASK-091 | Write unit tests for search ranking algorithm, tag suggestion logic, and Q&A retrieval strategy | | |
| TASK-092 | Write integration tests for search API with various filter combinations and semantic search scenarios | | |
| TASK-093 | Optimize vector search performance: tune HNSW/IVFFlat parameters, create appropriate indexes | | |
| TASK-094 | Document search modes, tag strategies, and Q&A usage patterns in user documentation | | |
| TASK-095 | Perform user acceptance testing for search relevance and Q&A answer quality | | |

### Implementation Phase 4: Polish, Observability & Production Readiness (2-3 weeks)

**GOAL-004**: Harden system for production deployment with comprehensive observability, performance optimization, and operational tooling

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-096 | Set up Seq or ELK stack for centralized log aggregation and configure Serilog sinks | | |
| TASK-097 | Configure structured logging with correlation IDs for request tracing across services | | |
| TASK-098 | Implement OpenTelemetry instrumentation for distributed tracing with Jaeger or Application Insights | | |
| TASK-099 | Set up Prometheus metrics exporters using prometheus-net in all API and background services | | |
| TASK-100 | Create Grafana dashboards for KPIs: GEMs created per day, summarization latency, LLM token usage, search queries, categorization accuracy | | |
| TASK-101 | Implement health check endpoints for all services: /health/live (liveness) and /health/ready (readiness) | | |
| TASK-102 | Configure ASP.NET Core health checks for database, Redis, MinIO, and LLM provider connectivity | | |
| TASK-103 | Profile application performance using dotnet-trace and identify top 5 bottlenecks | | |
| TASK-104 | Evaluate query performance for GEM list, search, and category views; introduce Dapper for high-latency paths if needed | | |
| TASK-105 | Implement response caching middleware for read-heavy API endpoints with appropriate cache headers | | |
| TASK-106 | Optimize database queries: add missing indexes, analyze query plans, implement query result caching | | |
| TASK-107 | Implement rate limiting for API endpoints to prevent abuse (per-user and global limits) | | |
| TASK-108 | Implement CORS configuration for Web UI and API with proper origin validation | | |
| TASK-109 | Conduct security audit: validate input sanitization, SQL injection prevention, XSS protection, CSRF tokens | | |
| TASK-110 | Implement data encryption at rest for sensitive fields (if not handled by PostgreSQL transparent encryption) | | |
| TASK-111 | Configure HTTPS/TLS for all services with automatic certificate management (Let's Encrypt) | | |
| TASK-112 | Create production Docker Compose configuration with optimized settings (resource limits, restart policies, networks) | | |
| TASK-113 | Write infrastructure-as-code for production deployment: Docker Compose files, nginx configuration, environment templates | | |
| TASK-114 | Implement database backup strategy: automated daily backups with retention policy and restore testing | | |
| TASK-115 | Create admin dashboard for operational monitoring: active jobs, failed jobs, system health, user statistics | | |
| TASK-116 | Implement graceful shutdown handling for background services to prevent data loss during container restarts | | |
| TASK-117 | Add database migration automation: run migrations on application startup with safety checks | | |
| TASK-118 | Create operational runbooks: deployment procedure, rollback procedure, backup/restore, troubleshooting guide | | |
| TASK-119 | Write load testing scripts using k6 or JMeter to validate performance under expected load (NFR-006: tens of GEMs per user per day) | | |
| TASK-120 | Conduct load testing and validate p95 latency meets NFR-001 (< 15 seconds for ingestion + summarization) | | |
| TASK-121 | Implement error alerting: configure alerts for critical errors, high latency, service downtime via email or Slack | | |
| TASK-122 | Create user documentation: getting started guide, feature overview, FAQ, troubleshooting | | |
| TASK-123 | Create developer documentation: architecture overview, development setup, contribution guidelines, API reference | | |
| TASK-124 | Conduct accessibility audit using axe DevTools and fix WCAG AA violations (NFR-005) | | |
| TASK-125 | Perform final end-to-end testing across all user journeys from Epic PRD | | |
| TASK-126 | Create release notes documenting features, known limitations, and migration guide (if applicable) | | |
| TASK-127 | Tag release version 1.0.0 in git and publish Docker images to registry | | |
| TASK-128 | Conduct retrospective and document lessons learned for future phases or epics | | |

## 3. Alternatives

### Alternative Approaches Considered

- **ALT-001**: **Microservices Architecture Instead of Modular Monolith** - Rejected because it adds operational complexity (service discovery, distributed transactions) without clear benefits at current scale. Modular monolith with Docker containers provides deployment flexibility and can be decomposed into microservices later if needed.

- **ALT-002**: **Separate Vector Database (Qdrant, Pinecone) Instead of pgvector** - Rejected to minimize infrastructure complexity. pgvector extension provides sufficient performance for expected scale and keeps all data in PostgreSQL, simplifying backup/restore and ACID transactions.

- **ALT-003**: **RabbitMQ or Azure Service Bus for Job Queue Instead of In-Memory Channels** - Deferred to future phases. In-memory queue with System.Threading.Channels is sufficient for self-hosted deployment. Can upgrade to distributed queue when scaling to SaaS multi-instance deployment.

- **ALT-004**: **Blazor WebAssembly SPA Instead of Razor Pages + HTMX** - Rejected because server-rendered UI with HTMX provides simpler architecture, better SEO, faster initial load, and easier accessibility compliance. Most interactions are CRUD-based and don't require rich client-side state management.

- **ALT-005**: **Hangfire for Background Jobs Instead of IHostedService** - Considered for future phases. IHostedService is lightweight and sufficient for Phase 1-3. Hangfire adds dashboard and advanced scheduling but increases dependencies. Can be added in Phase 4 if monitoring requirements justify it.

- **ALT-006**: **Local LLM (Ollama, LM Studio) as Primary Provider Instead of OpenAI** - Considered for self-hosted privacy-focused deployment. Kept as alternative implementation of ILLMProvider but not primary target due to quality and performance trade-offs. Cloud LLMs provide better summarization and categorization quality.

- **ALT-007**: **NoSQL Database (MongoDB) Instead of PostgreSQL** - Rejected because relational model fits GEM, Category, Tag relationships naturally. PostgreSQL with pgvector provides both relational integrity and vector search in single database.

- **ALT-008**: **GraphQL API Instead of REST** - Rejected to reduce complexity. REST APIs with OData filtering provide sufficient flexibility for current requirements. GraphQL adds schema complexity and client-side state management overhead.

## 4. Dependencies

### External Service Dependencies

- **DEP-001**: **LLM API Provider (OpenAI or Azure OpenAI)** - Critical dependency for summarization, categorization, tagging, and Q&A. Requires API key and sufficient quota. Mitigation: Implement provider abstraction to support fallback providers.

- **DEP-002**: **Embedding API Provider** - Required for semantic search. Can use OpenAI embeddings API or sentence-transformers local models. Mitigation: Cache embeddings and support multiple providers.

### Infrastructure Dependencies

- **DEP-003**: **PostgreSQL 16 with pgvector Extension** - Core data store. Must be available before Phase 1 development. Mitigation: Docker Compose configuration provides easy setup.

- **DEP-004**: **Redis** - Required for distributed caching and session management. Mitigation: Start with in-memory cache, add Redis in Phase 2.

- **DEP-005**: **MinIO or S3-Compatible Storage** - Required for storing web page snapshots. Mitigation: Use local file storage initially, migrate to MinIO in Phase 1.

- **DEP-006**: **Docker and Docker Compose** - Required for containerized deployment. Mitigation: None - fundamental requirement per CON-007.

### Library and Framework Dependencies

- **DEP-007**: **.NET 8.0 SDK** - Development environment requirement. Must be installed before development starts.

- **DEP-008**: **Playwright or PuppeteerSharp** - Headless browser for web scraping. Must be selected and configured in Phase 1 TASK-015.

- **DEP-009**: **Semantic Kernel or LangChain.NET** - LLM orchestration framework. Decision required in Phase 2 TASK-034.

- **DEP-010**: **Serilog** - Structured logging framework. Required from Phase 1.

- **DEP-011**: **Entity Framework Core 8.0** - ORM for data access. Required from Phase 1.

- **DEP-012**: **MediatR** - CQRS pattern library. Required from Phase 1.

- **DEP-013**: **FluentValidation** - Input validation library. Required from Phase 1.

- **DEP-014**: **Polly** - Resilience and fault handling. Required in Phase 2 for LLM API calls.

- **DEP-015**: **prometheus-net** - Metrics collection library. Required in Phase 4.

### Testing Dependencies

- **DEP-016**: **Testcontainers** - Integration testing with Docker containers. Required from Phase 1 for database integration tests.

- **DEP-017**: **xUnit, FluentAssertions, Moq** - Unit testing frameworks. Required from Phase 1.

- **DEP-018**: **k6 or JMeter** - Load testing tools. Required in Phase 4 for performance validation.

### Operational Dependencies

- **DEP-019**: **Seq or ELK Stack** - Log aggregation platform. Required in Phase 4 for centralized logging.

- **DEP-020**: **Grafana + Prometheus** - Metrics and dashboards. Required in Phase 4 for observability.

- **DEP-021**: **nginx or Traefik** - Reverse proxy and load balancer. Required for production deployment in Phase 4.

## 5. Files

### Solution Structure

- **FILE-001**: `InfoDumpManager.sln` - Main .NET solution file containing all projects
- **FILE-002**: `src/InfoDumpManager.Domain/InfoDumpManager.Domain.csproj` - Domain layer project with entities, aggregates, value objects, interfaces
- **FILE-003**: `src/InfoDumpManager.Application/InfoDumpManager.Application.csproj` - Application layer with MediatR handlers, DTOs, validators
- **FILE-004**: `src/InfoDumpManager.Infrastructure/InfoDumpManager.Infrastructure.csproj` - Infrastructure layer with EF Core, repositories, external services
- **FILE-005**: `src/InfoDumpManager.WebAPI/InfoDumpManager.WebAPI.csproj` - ASP.NET Core Web API project
- **FILE-006**: `src/InfoDumpManager.Web/InfoDumpManager.Web.csproj` - ASP.NET Core Razor Pages web application
- **FILE-007**: `tests/InfoDumpManager.Tests.Unit/InfoDumpManager.Tests.Unit.csproj` - Unit tests project
- **FILE-008**: `tests/InfoDumpManager.Tests.Integration/InfoDumpManager.Tests.Integration.csproj` - Integration tests project

### Domain Layer Files

- **FILE-009**: `src/InfoDumpManager.Domain/Entities/GEM.cs` - GEM aggregate root entity
- **FILE-010**: `src/InfoDumpManager.Domain/ValueObjects/GEMSource.cs` - Source URL and metadata value object
- **FILE-011**: `src/InfoDumpManager.Domain/ValueObjects/GEMSnapshot.cs` - Snapshot storage reference value object
- **FILE-012**: `src/InfoDumpManager.Domain/ValueObjects/GEMSummary.cs` - AI-generated summary value object
- **FILE-013**: `src/InfoDumpManager.Domain/Entities/Category.cs` - Category aggregate root
- **FILE-014**: `src/InfoDumpManager.Domain/Entities/Tag.cs` - Tag entity
- **FILE-015**: `src/InfoDumpManager.Domain/Entities/User.cs` - User entity (extends IdentityUser)
- **FILE-016**: `src/InfoDumpManager.Domain/Entities/ActivityLog.cs` - Activity log entity
- **FILE-017**: `src/InfoDumpManager.Domain/Repositories/IGEMRepository.cs` - GEM repository interface
- **FILE-018**: `src/InfoDumpManager.Domain/Repositories/ICategoryRepository.cs` - Category repository interface
- **FILE-019**: `src/InfoDumpManager.Domain/Repositories/ITagRepository.cs` - Tag repository interface
- **FILE-020**: `src/InfoDumpManager.Domain/Repositories/IActivityLogRepository.cs` - Activity log repository interface
- **FILE-021**: `src/InfoDumpManager.Domain/Services/ILLMProvider.cs` - LLM provider abstraction interface

### Application Layer Files

- **FILE-022**: `src/InfoDumpManager.Application/GEMs/Commands/CreateGEMCommand.cs` - MediatR command for creating GEM
- **FILE-023**: `src/InfoDumpManager.Application/GEMs/Commands/CreateGEMCommandHandler.cs` - Handler for CreateGEMCommand
- **FILE-024**: `src/InfoDumpManager.Application/GEMs/Commands/AssignCategoryCommand.cs` - Command for assigning category to GEM
- **FILE-025**: `src/InfoDumpManager.Application/GEMs/Queries/GetGEMByIdQuery.cs` - Query for retrieving GEM by ID
- **FILE-026**: `src/InfoDumpManager.Application/GEMs/Queries/SearchGEMsQuery.cs` - Query for searching GEMs
- **FILE-027**: `src/InfoDumpManager.Application/Categories/Commands/CreateCategoryCommand.cs` - Command for creating category
- **FILE-028**: `src/InfoDumpManager.Application/Tags/Commands/CreateTagCommand.cs` - Command for creating tag
- **FILE-029**: `src/InfoDumpManager.Application/GEMs/DTOs/GEMDto.cs` - Data transfer object for GEM
- **FILE-030**: `src/InfoDumpManager.Application/GEMs/Validators/CreateGEMCommandValidator.cs` - FluentValidation validator

### Infrastructure Layer Files

- **FILE-031**: `src/InfoDumpManager.Infrastructure/Data/ApplicationDbContext.cs` - EF Core DbContext
- **FILE-032**: `src/InfoDumpManager.Infrastructure/Data/Configurations/GEMConfiguration.cs` - EF Core entity configuration for GEM
- **FILE-033**: `src/InfoDumpManager.Infrastructure/Migrations/` - Directory containing EF Core migrations
- **FILE-034**: `src/InfoDumpManager.Infrastructure/Repositories/GEMRepository.cs` - GEM repository implementation
- **FILE-035**: `src/InfoDumpManager.Infrastructure/Repositories/CategoryRepository.cs` - Category repository implementation
- **FILE-036**: `src/InfoDumpManager.Infrastructure/Services/WebScrapingService.cs` - Web scraping implementation with Playwright
- **FILE-037**: `src/InfoDumpManager.Infrastructure/Services/OpenAILLMProvider.cs` - OpenAI provider implementation
- **FILE-038**: `src/InfoDumpManager.Infrastructure/Services/AzureOpenAILLMProvider.cs` - Azure OpenAI provider implementation
- **FILE-039**: `src/InfoDumpManager.Infrastructure/Services/LLMOrchestrationService.cs` - LLM orchestration with Semantic Kernel
- **FILE-040**: `src/InfoDumpManager.Infrastructure/Services/MinioStorageService.cs` - MinIO object storage service
- **FILE-041**: `src/InfoDumpManager.Infrastructure/BackgroundServices/SummarizationBackgroundService.cs` - Summarization worker
- **FILE-042**: `src/InfoDumpManager.Infrastructure/BackgroundServices/CategorizationBackgroundService.cs` - Categorization worker
- **FILE-043**: `src/InfoDumpManager.Infrastructure/BackgroundServices/TaggingBackgroundService.cs` - Tagging worker

### API Layer Files

- **FILE-044**: `src/InfoDumpManager.WebAPI/Controllers/GEMsController.cs` - GEM API endpoints
- **FILE-045**: `src/InfoDumpManager.WebAPI/Controllers/CategoriesController.cs` - Category API endpoints
- **FILE-046**: `src/InfoDumpManager.WebAPI/Controllers/TagsController.cs` - Tag API endpoints
- **FILE-047**: `src/InfoDumpManager.WebAPI/Controllers/SearchController.cs` - Search API endpoints
- **FILE-048**: `src/InfoDumpManager.WebAPI/Controllers/QueryController.cs` - Q&A and synthesis endpoints
- **FILE-049**: `src/InfoDumpManager.WebAPI/Middleware/ErrorHandlingMiddleware.cs` - Global error handling
- **FILE-050**: `src/InfoDumpManager.WebAPI/Program.cs` - Application entry point and configuration
- **FILE-051**: `src/InfoDumpManager.WebAPI/appsettings.json` - Configuration settings
- **FILE-052**: `src/InfoDumpManager.WebAPI/appsettings.Development.json` - Development configuration

### Web UI Files

- **FILE-053**: `src/InfoDumpManager.Web/Pages/Index.cshtml` - Home page with GEM submission form
- **FILE-054**: `src/InfoDumpManager.Web/Pages/GEMs/List.cshtml` - GEM list page with filtering
- **FILE-055**: `src/InfoDumpManager.Web/Pages/GEMs/Detail.cshtml` - GEM detail page
- **FILE-056**: `src/InfoDumpManager.Web/Pages/Categories/Manage.cshtml` - Category management page
- **FILE-057**: `src/InfoDumpManager.Web/Pages/Tags/Manage.cshtml` - Tag management page
- **FILE-058**: `src/InfoDumpManager.Web/Pages/Search/Index.cshtml` - Search page
- **FILE-059**: `src/InfoDumpManager.Web/Pages/Categories/View.cshtml` - Category view with Q&A interface

### Configuration and Infrastructure Files

- **FILE-060**: `docker-compose.yml` - Development Docker Compose configuration
- **FILE-061**: `docker-compose.prod.yml` - Production Docker Compose configuration
- **FILE-062**: `Dockerfile.webapi` - Dockerfile for Web API service
- **FILE-063**: `Dockerfile.web` - Dockerfile for Web UI service
- **FILE-064**: `.dockerignore` - Docker ignore file
- **FILE-065**: `nginx/nginx.conf` - Nginx reverse proxy configuration
- **FILE-066**: `.env.template` - Environment variables template
- **FILE-067**: `scripts/init-db.sql` - Database initialization script

### Documentation Files

- **FILE-068**: `README.md` - Project overview and getting started guide
- **FILE-069**: `docs/api.md` - API documentation with examples
- **FILE-070**: `docs/architecture.md` - Architecture documentation
- **FILE-071**: `docs/development.md` - Development setup and contribution guide
- **FILE-072**: `docs/deployment.md` - Deployment procedures and runbooks
- **FILE-073**: `docs/prompts/summarization-v1.md` - Summarization prompt template documentation
- **FILE-074**: `docs/prompts/categorization-v1.md` - Categorization prompt template documentation

## 6. Testing

### Unit Testing Strategy

- **TEST-001**: **Domain Entity Validation Tests** - Test all domain entities for invariant enforcement, value object immutability, and business rule validation using xUnit and FluentAssertions. Target: 90%+ coverage of domain layer.

- **TEST-002**: **Command Handler Tests** - Test all MediatR command handlers with mocked dependencies (repositories, services) to verify business logic, validation, and error handling. Use Moq for mocking.

- **TEST-003**: **Query Handler Tests** - Test all MediatR query handlers to verify correct data retrieval, filtering, pagination, and mapping logic.

- **TEST-004**: **Validator Tests** - Test all FluentValidation validators with valid and invalid inputs to ensure comprehensive input validation coverage.

- **TEST-005**: **LLM Provider Abstraction Tests** - Test LLM provider implementations with mocked HTTP responses to verify prompt construction, response parsing, error handling, and retry logic.

- **TEST-006**: **Search Ranking Algorithm Tests** - Test hybrid search ranking with predefined relevance scores to verify correct score combination and result ordering.

### Integration Testing Strategy

- **TEST-007**: **Database Integration Tests** - Use Testcontainers to spin up PostgreSQL instance and test repository implementations, migrations, and complex queries. Verify data persistence, retrieval, and concurrency handling.

- **TEST-008**: **API Endpoint Integration Tests** - Test all Web API controllers with WebApplicationFactory to verify request/response handling, authentication, authorization, validation errors, and HTTP status codes.

- **TEST-009**: **Background Service Integration Tests** - Test background job processing end-to-end: enqueue job → worker processes → database updated → verify results. Use Testcontainers for Redis and PostgreSQL.

- **TEST-010**: **Web Scraping Service Tests** - Test web scraping service with local HTML test pages to verify content extraction, error handling for invalid URLs, and snapshot storage.

- **TEST-011**: **Vector Search Integration Tests** - Test pgvector similarity search with known embedding vectors and verify top-k retrieval accuracy and performance.

- **TEST-012**: **LLM Integration Tests** - Test end-to-end AI workflows (summarization, categorization, tagging) with real LLM API calls (use low-cost models or test environment). Verify response parsing and database updates.

### Performance Testing Strategy

- **TEST-013**: **Load Testing** - Use k6 to simulate concurrent users creating GEMs, searching, and querying. Verify system handles NFR-006 (tens of GEMs per user per day) without degradation. Target: 100 concurrent users with 95% success rate.

- **TEST-014**: **Latency Testing** - Measure p50, p95, p99 latencies for critical paths: GEM ingestion + summarization (must meet NFR-001 < 15s p95), search queries, category synthesis. Use k6 or JMeter.

- **TEST-015**: **Database Performance Testing** - Profile slow queries using pgBench and query analysis tools. Verify indexes are used correctly and query plans are optimal.

- **TEST-016**: **Vector Search Performance Testing** - Test pgvector search performance with 1k, 10k, 100k embeddings to determine scaling characteristics and tune HNSW/IVFFlat parameters.

### End-to-End Testing Strategy

- **TEST-017**: **User Journey Testing** - Manually test all user journeys from Epic PRD: Add web source → view summary → accept category → apply tags → search → ask question. Verify complete workflows.

- **TEST-018**: **Accessibility Testing** - Use axe DevTools to audit all UI pages for WCAG AA compliance. Test keyboard navigation, screen reader compatibility, color contrast, and ARIA labels.

- **TEST-019**: **Cross-Browser Testing** - Test web UI in Chrome, Firefox, Safari, and Edge to verify rendering, JavaScript functionality, and responsive design.

- **TEST-020**: **Error Scenario Testing** - Test failure scenarios: LLM API down, database connection lost, invalid URLs, malformed responses. Verify graceful degradation and error messages.

### Test Data Management

- **TEST-021**: **Test Data Generation** - Use Bogus library to generate realistic test data (GEMs, categories, tags, users) for development and testing environments.

- **TEST-022**: **Database Seeding** - Create seed data scripts for development environment with sample categories and GEMs to facilitate manual testing and demos.

## 7. Risks & Assumptions

### Technical Risks

- **RISK-001**: **LLM API Rate Limiting and Costs** - High volume of summarization and categorization requests may exceed API rate limits or budget. **Mitigation**: Implement request throttling, caching, and cost monitoring with alerts. Consider tier-based batching for non-urgent jobs.

- **RISK-002**: **LLM Quality Variability** - AI-generated summaries and categorizations may have inconsistent quality, requiring manual corrections. **Mitigation**: Implement prompt versioning, A/B testing of prompts, user feedback collection, and confidence thresholds to flag low-quality outputs.

- **RISK-003**: **Web Scraping Failures** - Some websites may block headless browsers, use CAPTCHAs, or have complex JavaScript rendering. **Mitigation**: Implement retry logic, user-agent rotation, fallback to simple HTTP fetching, and allow manual HTML upload.

- **RISK-004**: **Vector Search Performance at Scale** - pgvector performance may degrade with large embedding datasets (100k+ vectors). **Mitigation**: Profile vector search early in Phase 3, optimize index parameters, and plan migration to dedicated vector database (Qdrant) if needed.

- **RISK-005**: **Background Job Processing Bottlenecks** - Single-instance background workers may not keep up with high ingestion rates. **Mitigation**: Implement job queue monitoring, horizontal scaling support (migrate to RabbitMQ if needed), and prioritization logic.

- **RISK-006**: **Database Migration Failures** - Complex schema changes in production may cause downtime or data loss. **Mitigation**: Test all migrations in staging, implement rollback procedures, and use online migration strategies for large tables.

- **RISK-007**: **Security Vulnerabilities** - Exposure of LLM API keys, SQL injection, XSS attacks, or unauthorized data access. **Mitigation**: Conduct security audit in Phase 4, use parameterized queries, implement CSRF protection, and follow OWASP best practices.

### Project Risks

- **RISK-008**: **Scope Creep** - Additional features (email ingestion, PDF processing, team collaboration) may be requested during development. **Mitigation**: Strictly adhere to Epic PRD scope, document out-of-scope items in backlog for future phases.

- **RISK-009**: **LLM Provider Availability** - Dependence on OpenAI or Azure OpenAI creates vendor lock-in. **Mitigation**: Implement provider abstraction (ILLMProvider) to support multiple providers and local models.

- **RISK-010**: **Team Availability** - Team members may be unavailable due to other commitments, delaying phases. **Mitigation**: Build knowledge sharing through documentation, code reviews, and cross-training on critical components.

### Operational Risks

- **RISK-011**: **Production Deployment Complexity** - Docker Compose orchestration, secrets management, and certificate configuration may have unexpected issues. **Mitigation**: Test production deployment configuration in staging environment, create detailed runbooks, and use infrastructure-as-code.

- **RISK-012**: **Data Loss or Corruption** - Database failures, backup failures, or bugs in data migration may cause data loss. **Mitigation**: Implement automated backups with verification, test restore procedures, and maintain audit logs for data recovery.

- **RISK-013**: **Monitoring Blind Spots** - Inadequate observability may delay detection of performance issues or outages. **Mitigation**: Implement comprehensive logging, metrics, tracing, and alerts in Phase 4. Test alerting thresholds before production launch.

### Assumptions

- **ASSUMPTION-001**: **LLM API Access** - Assumes valid API keys and sufficient quota for OpenAI or Azure OpenAI are available throughout development and testing.

- **ASSUMPTION-002**: **Single-User Initially** - Assumes initial deployment will be single-user self-hosted, though architecture supports multi-tenancy for future SaaS migration.

- **ASSUMPTION-003**: **English Content Only** - Assumes all ingested content will be in English. Multi-language support is out of scope for this epic.

- **ASSUMPTION-004**: **Moderate Scale** - Assumes each user will save tens of GEMs per day (NFR-006), not hundreds. High-volume scenarios may require architecture adjustments.

- **ASSUMPTION-005**: **Modern Browser Support** - Assumes users will use modern browsers (Chrome, Firefox, Safari, Edge) with JavaScript enabled. Legacy browser support (IE11) is out of scope.

- **ASSUMPTION-006**: **Development Environment** - Assumes developers have Docker Desktop, .NET 8 SDK, and sufficient local resources (8GB+ RAM) for running containerized services.

- **ASSUMPTION-007**: **Network Connectivity** - Assumes reliable internet connectivity for LLM API calls and web scraping. Offline mode is out of scope.

- **ASSUMPTION-008**: **Legal Web Scraping** - Assumes users will only ingest content they have legal right to access and store. Copyright compliance is user's responsibility.

- **ASSUMPTION-009**: **No Real-Time Requirements** - Assumes async processing with eventual consistency is acceptable. Real-time summarization and categorization are not required.

- **ASSUMPTION-010**: **Prompt Engineering Success** - Assumes effective prompts for summarization, categorization, and tagging can be developed within estimated time. May require additional iteration if quality is insufficient.

## 8. Related Specifications / Further Reading

### Internal Documentation

- [Epic PRD: GEM Ingestion, Summarization, and Smart Categorization](epic.md) - Product requirements document defining business goals and user journeys
- [Epic Architecture Specification](arch.md) - High-level technical architecture and technology stack decisions
- [App Overview](App%20Overview.md) - Original application concept and vision

### External References

#### Domain-Driven Design

- [Domain-Driven Design by Eric Evans](https://www.domainlanguage.com/ddd/) - Foundational DDD concepts
- [Implementing Domain-Driven Design by Vaughn Vernon](https://vaughnvernon.com/) - Practical DDD implementation patterns

#### .NET and ASP.NET Core

- [ASP.NET Core Documentation](https://docs.microsoft.com/en-us/aspnet/core/) - Official ASP.NET Core guides
- [Entity Framework Core Documentation](https://docs.microsoft.com/en-us/ef/core/) - EF Core reference
- [.NET Architecture Guides](https://dotnet.microsoft.com/learn/dotnet/architecture-guides) - Microsoft architecture patterns

#### AI and LLM Integration

- [Semantic Kernel Documentation](https://learn.microsoft.com/en-us/semantic-kernel/) - LLM orchestration framework
- [OpenAI API Documentation](https://platform.openai.com/docs/) - OpenAI API reference
- [Retrieval-Augmented Generation (RAG) Paper](https://arxiv.org/abs/2005.11401) - RAG pattern for Q&A systems

#### Vector Databases

- [pgvector GitHub Repository](https://github.com/pgvector/pgvector) - PostgreSQL vector extension
- [HNSW Algorithm](https://arxiv.org/abs/1603.09320) - Hierarchical Navigable Small World graph for vector search
- [Vector Database Comparison](https://github.com/erikbern/ann-benchmarks) - Benchmark of vector search solutions

#### Background Processing

- [ASP.NET Core Background Tasks](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services) - IHostedService documentation
- [System.Threading.Channels](https://devblogs.microsoft.com/dotnet/an-introduction-to-system-threading-channels/) - Producer-consumer patterns

#### Testing

- [xUnit Documentation](https://xunit.net/) - Unit testing framework
- [Testcontainers Documentation](https://dotnet.testcontainers.org/) - Integration testing with containers
- [k6 Documentation](https://k6.io/docs/) - Load testing tool

#### Observability

- [Serilog Documentation](https://serilog.net/) - Structured logging
- [OpenTelemetry .NET](https://opentelemetry.io/docs/instrumentation/net/) - Distributed tracing
- [Prometheus .NET Client](https://github.com/prometheus-net/prometheus-net) - Metrics collection

#### Security

- [OWASP Top 10](https://owasp.org/www-project-top-ten/) - Web application security risks
- [ASP.NET Core Security Best Practices](https://docs.microsoft.com/en-us/aspnet/core/security/) - Security guidance

#### Deployment

- [Docker Documentation](https://docs.docker.com/) - Container platform
- [Docker Compose Documentation](https://docs.docker.com/compose/) - Multi-container orchestration
- [nginx Documentation](https://nginx.org/en/docs/) - Reverse proxy configuration

---

**End of Implementation Plan v1.0**