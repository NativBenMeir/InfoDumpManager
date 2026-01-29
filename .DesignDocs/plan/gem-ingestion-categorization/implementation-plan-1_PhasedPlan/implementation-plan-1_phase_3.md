---
goal: Implementation Plan for GEM Ingestion, Summarization, and Smart Categorization System
phase_title: Domain Model & Repository Pattern Implementation
PhaseNumber: 3
version: 1.1
date_created: 2026-01-28
last_updated: 2026-01-28
tags: [domain, repository, entities, value-objects, ddd]
depends_on: [1, 2]
status: Completed
status_color: brightgreen
---

# Introduction

![Status: Completed](https://img.shields.io/badge/Status-Completed-brightgreen)

This phase implements the core domain model following domain-driven design principles. It creates the GEM and Category aggregates with their entities and value objects, implements repository interfaces in the domain layer, and provides concrete repository implementations in the infrastructure layer using Entity Framework Core. The phase also establishes the Unit of Work pattern for transaction management.

## 1. Requirements & Constraints

- **REQ-001**: System must ingest web pages via URL submission with headless browser rendering
- **REQ-010**: System must store original web page snapshots with source links
- **CON-001**: Must use .NET 8.0 LTS as primary framework
- **CON-004**: Must follow domain-driven design with clear layer separation
- **CON-005**: Must support both self-hosted (Docker Compose) and future SaaS (K8s-ready) deployment
- **CON-006**: Must use Entity Framework Core for Phase 1-3
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
| TASK-003 | Design and implement GEM Aggregate in Domain layer with entities: GEM, GEMSource (value object), GEMSnapshot (value object), GEMSummary (value object) | Completed | 2026-01-29 |
| TASK-004 | Design and implement Category Aggregate with Category entity and GEM assignments | Completed | 2026-01-29 |
| TASK-008 | Implement Repository interfaces in Domain layer (IGEMRepository, ICategoryRepository, IActivityLogRepository) | Completed | 2026-01-29 |
| TASK-009 | Implement concrete repositories in Infrastructure layer using EF Core DbContext | Completed | 2026-01-29 |
| TASK-010 | Implement Unit of Work pattern in Infrastructure layer | Completed | 2026-01-29 |
| TASK-026 | Write unit tests using xUnit v3 for domain entities, value objects, and validation logic (target: 80% coverage) | Completed | 2026-01-29 |
| TASK-027 | Write integration tests for GEM and Category API endpoints using Testcontainers 4.10.0 for PostgreSQL | Completed | 2026-01-29 |
| TASK-036-P3 | Implement domain validation rules for GEM aggregate (URL validation, required fields) | Completed | 2026-01-29 |
| TASK-037-P3 | Implement domain validation rules for Category aggregate (name uniqueness, hierarchy constraints) | Completed | 2026-01-29 |
| TASK-TST-P3 | Implement all tests based on per Testing section in this plan. | Completed | 2026-01-29 |
| TASK-AUT | Implement all unit tests based on Testing section in this plan | Completed | 2026-01-29 |
| TASK-AIT | Implement all integration tests based on Testing section in this plan | Completed | 2026-01-29 |

## 3. Alternatives

- **ALT-007**: NoSQL Database (MongoDB) Instead of PostgreSQL - Rejected because relational model fits GEM, Category, Tag relationships naturally
- **ALT-001**: Microservices Architecture Instead of Modular Monolith - Rejected to reduce operational complexity at current scale

## 4. Dependencies

- **PHASE-DEP-002**: Requires database schema from Phase 2 - Verify all tables exist and migrations applied
- **DEP-002**: `src/InfoDumpManager.Domain/InfoDumpManager.Domain.csproj` - Domain layer project
- **DEP-004**: `src/InfoDumpManager.Infrastructure/InfoDumpManager.Infrastructure.csproj` - Infrastructure layer
- **DEP-011**: Entity Framework Core 8.0.x - ORM for data access
- **DEP-017**: xUnit v3, FluentAssertions 8.8.0, Moq 4.20.72 - Unit testing frameworks

## 5. Files

- **FILE-009**: `src/InfoDumpManager.Domain/Entities/GEM.cs` - GEM aggregate root entity
- **FILE-010**: `src/InfoDumpManager.Domain/ValueObjects/GEMSource.cs` - Source URL and metadata value object
- **FILE-011**: `src/InfoDumpManager.Domain/ValueObjects/GEMSnapshot.cs` - Snapshot storage reference value object
- **FILE-012**: `src/InfoDumpManager.Domain/ValueObjects/GEMSummary.cs` - AI-generated summary value object
- **FILE-013**: `src/InfoDumpManager.Domain/Entities/Category.cs` - Category aggregate root
- **FILE-017**: `src/InfoDumpManager.Domain/Repositories/IGEMRepository.cs` - GEM repository interface
- **FILE-018**: `src/InfoDumpManager.Domain/Repositories/ICategoryRepository.cs` - Category repository interface
- **FILE-020**: `src/InfoDumpManager.Domain/Repositories/IActivityLogRepository.cs` - Activity log repository interface
- **FILE-034**: `src/InfoDumpManager.Infrastructure/Repositories/GEMRepository.cs` - GEM repository implementation
- **FILE-035**: `src/InfoDumpManager.Infrastructure/Repositories/CategoryRepository.cs` - Category repository implementation
- **FILE-035-P3**: `src/InfoDumpManager.Infrastructure/Repositories/ActivityLogRepository.cs` - Activity log repository implementation
- **FILE-035-P3**: `src/InfoDumpManager.Infrastructure/Repositories/UnitOfWork.cs` - Unit of Work implementation

## 6. Testing

- **TEST-013**: Unit Test - GEM Entity - Create GEM with valid data - Expected: GEM created successfully
- **TEST-014**: Unit Test - GEM Entity - Create GEM with invalid URL - Expected: Validation exception thrown
- **TEST-015**: Unit Test - GEMSource Value Object - Equality comparison - Expected: Two instances with same URL are equal
- **TEST-016**: Unit Test - Category Entity - Create category with valid name - Expected: Category created successfully
- **TEST-017**: Integration Test - GEM Repository - Insert and retrieve GEM - Expected: Retrieved GEM matches inserted data
- **TEST-018**: Integration Test - Category Repository - Query categories by name - Expected: Correct category returned
- **TEST-019**: Integration Test - Unit of Work - Multi-entity transaction - Expected: All changes committed or rolled back together
- **TEST-020**: Unit Test - Domain Validation - GEM with empty title - Expected: Validation error

### Test Requirements
- Unit tests for all domain entities with 80% code coverage
- Integration tests for all repository operations using Testcontainers
- All validation rules must be tested with valid and invalid inputs
- Transaction rollback scenarios must be tested

## 7. Risks & Assumptions

- **RISK-005**: Complex domain validation logic may impact performance - Mitigation: Keep validation lightweight, move heavy processing to application layer
- **RISK-006**: Repository abstractions may leak infrastructure concerns - Mitigation: Strictly enforce domain layer isolation, review interfaces carefully
- **ASSUMPTION-006**: Value objects are immutable and compared by value
- **ASSUMPTION-007**: Aggregate roots are the only entry points for modifying entities within the aggregate

## 8. Success Metrics

- **METRIC-002**: All TEST-XXX tests passing (exit code 0)
- **METRIC-003**: Build successful with no errors (exit code 0)
- **METRIC-004**: Code coverage ≥80% for domain layer
- **METRIC-011**: Zero domain layer dependencies on infrastructure layer (dependency inversion verified)
- **METRIC-012**: All repository operations support async/await pattern
- **METRIC-013**: Unit tests execute in <5 seconds total

## 9. Related Specifications / Further Reading

- [Domain-Driven Design by Eric Evans](https://www.domainlanguage.com/ddd/)
- [Repository Pattern in .NET](https://docs.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design)
- [Value Objects in DDD](https://enterprisecraftsmanship.com/posts/value-objects-explained/)
