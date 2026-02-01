---
goal: Implementation Plan for GEM Ingestion, Summarization, and Smart Categorization System
phase_title: Web Scraping & Basic GEM CRUD Operations
PhaseNumber: 5
version: 1.1
date_created: 2026-01-28
last_updated: 2026-01-28
tags: [web-scraping, ingestion, storage, playwright, minio]
depends_on: [1, 2, 3, 4]
status: Completed
status_color: brightgreen
---

# Introduction

![Status: Completed](https://img.shields.io/badge/Status-Completed-brightgreen)

This phase implements the web scraping and content ingestion pipeline. It integrates Playwright for headless browser rendering, implements the web scraping service with URL validation and HTML cleaning, and sets up MinIO object storage for storing web page snapshots. The phase delivers the complete GEM creation workflow from URL submission to persistent storage with activity logging.

## 1. Requirements & Constraints

- **REQ-001**: System must ingest web pages via URL submission with headless browser rendering
- **REQ-010**: System must store original web page snapshots with source links
- **CON-001**: Must use .NET 8.0 LTS as primary framework
- **CON-004**: Must follow domain-driven design with clear layer separation
- **CON-005**: Must support both self-hosted (Docker Compose) and future SaaS (K8s-ready) deployment
- **NFR-001**: Ingestion + summarization must complete in < 15 seconds (p95) for typical web pages
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
| TASK-015 | Implement Web Scraping Service using Playwright with URL validation, content fetching, and HTML cleaning | Yes | 2026-01-30 |
| TASK-016 | Implement MinIO integration for storing web page snapshots (HTML) in object storage | Yes | 2026-01-30 |
| TASK-025-P5 | Implement query handlers: GetGEMByIdQuery, ListGEMsQuery with pagination support | Yes | 2026-01-30 |
| TASK-038-P5 | Implement Polly 8.6.5 retry policies for web scraping with exponential backoff | Yes | 2026-01-30 |
| TASK-039-P5 | Implement circuit breaker for web scraping to handle repeated failures gracefully | Yes | 2026-01-30 |
| TASK-040-P5 | Add activity logging for GEM creation events (GEMCreated, GEMUpdated) | Yes | 2026-01-30 |
| TASK-041-P5 | Implement URL validation and normalization in web scraping service | Yes | 2026-01-30 |
| TASK-042-P5 | Implement HTML sanitization to remove scripts and unsafe content from snapshots | Yes | 2026-01-30 |
| TASK-043-P5 | Create integration tests for web scraping service using mock web server | Yes | 2026-01-30 |
| TASK-TST-P5 | Implement all tests based on per Testing section in this plan. | Yes | 2026-01-30 |

## 3. Alternatives

- **ALT-006**: Local LLM (Ollama, LM Studio) as Primary Provider Instead of OpenAI - Deferred to future phases
- **ALT-002**: Separate Vector Database (Qdrant, Pinecone) Instead of pgvector - Rejected to minimize infrastructure complexity
- **ALT-005**: Hangfire for Background Jobs Instead of IHostedService - Considered for future phases

## 4. Dependencies

- **PHASE-DEP-004**: Requires API foundation from Phase 4 - Verify CreateGEMCommand handler exists
- **PHASE-DEP-005**: Requires MinIO container from Phase 1 - Verify MinIO is accessible
- **DEP-005**: MinIO or S3-Compatible Storage - Required for storing web page snapshots
- **DEP-008**: Playwright or PuppeteerSharp - Headless browser for web scraping
- **DEP-014**: Polly 8.6.5 - Resilience and fault handling

## 5. Files

- **FILE-036**: `src/InfoDumpManager.Infrastructure/Services/WebScrapingService.cs` - Web scraping implementation with Playwright
- **FILE-040**: `src/InfoDumpManager.Infrastructure/Services/MinioStorageService.cs` - MinIO object storage service
- **FILE-040-P5**: `src/InfoDumpManager.Infrastructure/Services/IStorageService.cs` - Storage service abstraction interface
- **FILE-025**: `src/InfoDumpManager.Application/GEMs/Queries/GetGEMByIdQuery.cs` - Query for retrieving GEM by ID
- **FILE-025-P5**: `src/InfoDumpManager.Application/GEMs/Queries/GetGEMByIdQueryHandler.cs` - Handler for GetGEMByIdQuery
- **FILE-026**: `src/InfoDumpManager.Application/GEMs/Queries/SearchGEMsQuery.cs` - Query for searching GEMs
- **FILE-026-P5**: `src/InfoDumpManager.Application/GEMs/Queries/ListGEMsQuery.cs` - Query for listing GEMs with pagination

## 6. Testing

- **TEST-030**: Integration Test - Web Scraping - Fetch valid URL - Expected: HTML content retrieved and cleaned
- **TEST-031**: Integration Test - Web Scraping - Fetch invalid URL - Expected: Proper error handling with retry
- **TEST-032**: Integration Test - Web Scraping - Timeout scenario - Expected: Circuit breaker opens after threshold
- **TEST-033**: Integration Test - MinIO Storage - Upload snapshot - Expected: Snapshot stored with correct key
- **TEST-034**: Integration Test - MinIO Storage - Retrieve snapshot - Expected: Original HTML returned
- **TEST-035**: Integration Test - GEM Creation - End-to-end URL to storage - Expected: GEM created with snapshot reference
- **TEST-036**: Unit Test - URL Validation - Valid URLs - Expected: Accepted
- **TEST-037**: Unit Test - URL Validation - Invalid URLs - Expected: Rejected with error
- **TEST-038**: Unit Test - HTML Sanitization - Script tags removed - Expected: Clean HTML output

### Test Requirements
- Web scraping must be tested with mock HTTP server
- MinIO integration tests must use Testcontainers
- Circuit breaker behavior must be validated under failure scenarios
- All error paths must have test coverage

## 7. Risks & Assumptions

- **RISK-009**: Web pages with heavy JavaScript may not render correctly - Mitigation: Use Playwright with JavaScript execution enabled
- **RISK-010**: Some websites may block or rate-limit scraping - Mitigation: Implement user-agent rotation and respect robots.txt
- **RISK-011**: Large web pages may exceed storage limits - Mitigation: Implement size limits and compression
- **ASSUMPTION-010**: Most target web pages render within 10 seconds
- **ASSUMPTION-011**: MinIO bucket is pre-created or service creates it on first use

## 8. Success Metrics

- **METRIC-002**: All TEST-XXX tests passing (exit code 0)
- **METRIC-003**: Build successful with no errors (exit code 0)
- **METRIC-018**: Web scraping completes within 10 seconds for typical pages (p95)
- **METRIC-019**: Circuit breaker opens after 5 consecutive failures
- **METRIC-020**: Snapshots successfully stored in MinIO and retrievable
- **METRIC-021**: URL validation rejects malformed URLs before scraping

## 9. Related Specifications / Further Reading

- [Playwright for .NET Documentation](https://playwright.dev/dotnet/)
- [MinIO .NET SDK](https://docs.min.io/docs/dotnet-client-quickstart-guide.html)
- [Polly Resilience Documentation](https://github.com/App-vNext/Polly)
- [HTML Sanitization Best Practices](https://cheatsheetseries.owasp.org/cheatsheets/Cross_Site_Scripting_Prevention_Cheat_Sheet.html)
