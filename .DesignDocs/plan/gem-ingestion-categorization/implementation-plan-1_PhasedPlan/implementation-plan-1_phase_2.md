---
goal: Implementation Plan for GEM Ingestion, Summarization, and Smart Categorization System
phase_title: Database Schema & Entity Framework Configuration
PhaseNumber: 2
version: 1.1
date_created: 2026-01-28
last_updated: 2026-01-28
tags: [database, schema, entity-framework, migration, infrastructure]
depends_on: [1]
status: Planned
status_color: blue
---

# Introduction

![Status: Planned](https://img.shields.io/badge/Status-Planned-blue)

This phase establishes the database foundation by designing and implementing the PostgreSQL schema using Entity Framework Core migrations. It creates the core tables (GEM, Category, User, ActivityLog) with proper indexing, foreign keys, and constraints. The phase also configures the ApplicationDbContext and entity configurations following EF Core best practices.

## 1. Requirements & Constraints

- **REQ-001**: System must ingest web pages via URL submission with headless browser rendering
- **REQ-009**: System must maintain activity logs for all GEM operations and AI actions
- **CON-001**: Must use .NET 10.0.2 LTS as primary framework
- **CON-002**: Must use PostgreSQL 16.11 with pgvector extension for data persistence
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
| TASK-005 | Design and implement User entity with ASP.NET Core Identity integration | | |
| TASK-006 | Design and implement ActivityLog entity for audit trail with event types (GEMCreated, GEMUpdated, CategoryAssigned, etc.) | | |
| TASK-007 | Create PostgreSQL schema with EF Core migrations for GEM, Category, User, ActivityLog tables with proper indexes | | |
| TASK-031-P2 | Create EF Core DbContext with entity configurations and relationships | | |
| TASK-032-P2 | Configure entity type configurations for GEM aggregate with value object mapping | | |
| TASK-033-P2 | Configure entity type configurations for Category entity with navigation properties | | |
| TASK-034-P2 | Configure entity type configurations for ActivityLog with JSON column for metadata | | |
| TASK-035-P2 | Create initial EF Core migration and verify SQL generation is correct | | |

## 3. Alternatives

- **ALT-007**: NoSQL Database (MongoDB) Instead of PostgreSQL - Rejected because relational model fits GEM, Category, Tag relationships naturally
- **ALT-002**: Separate Vector Database (Qdrant, Pinecone) Instead of pgvector - Rejected to minimize infrastructure complexity

## 4. Dependencies

- **PHASE-DEP-001**: Requires Docker Compose environment from Phase 1 - Verify PostgreSQL container is running
- **DEP-003**: PostgreSQL 16.11 with pgvector Extension - Core data store
- **DEP-011**: Entity Framework Core 10.0.2 - ORM for data access

## 5. Files

- **FILE-031**: `src/InfoDumpManager.Infrastructure/Data/ApplicationDbContext.cs` - EF Core DbContext
- **FILE-032**: `src/InfoDumpManager.Infrastructure/Data/Configurations/GEMConfiguration.cs` - EF Core entity configuration for GEM
- **FILE-032-P2**: `src/InfoDumpManager.Infrastructure/Data/Configurations/CategoryConfiguration.cs` - EF Core entity configuration for Category
- **FILE-032-P2**: `src/InfoDumpManager.Infrastructure/Data/Configurations/UserConfiguration.cs` - EF Core entity configuration for User
- **FILE-032-P2**: `src/InfoDumpManager.Infrastructure/Data/Configurations/ActivityLogConfiguration.cs` - EF Core entity configuration for ActivityLog
- **FILE-033**: `src/InfoDumpManager.Infrastructure/Migrations/` - Directory containing EF Core migrations
- **FILE-015**: `src/InfoDumpManager.Domain/Entities/User.cs` - User entity (extends IdentityUser)
- **FILE-016**: `src/InfoDumpManager.Domain/Entities/ActivityLog.cs` - Activity log entity

## 6. Testing

- **TEST-007**: Integration Test - EF Core Context - Verify DbContext can connect to PostgreSQL - Expected: Successful connection
- **TEST-008**: Integration Test - Migrations - Apply migrations to test database - Expected: All tables created successfully
- **TEST-009**: Integration Test - Entity Configurations - Verify GEM entity mapping is correct - Expected: Can insert and retrieve GEM
- **TEST-010**: Integration Test - Entity Configurations - Verify Category entity mapping is correct - Expected: Can insert and retrieve Category
- **TEST-011**: Integration Test - Foreign Keys - Verify GEM-Category relationship enforced - Expected: Cannot delete Category with assigned GEMs
- **TEST-012**: Integration Test - Indexes - Verify indexes created on commonly queried columns - Expected: Query plan uses indexes

### Test Requirements
- All migrations must apply successfully without errors
- Integration tests must use Testcontainers for PostgreSQL
- All entity configurations must be tested with insert/update/delete operations
- Foreign key constraints must be validated

## 7. Risks & Assumptions

- **RISK-003**: EF Core migration conflicts if multiple developers create migrations simultaneously - Mitigation: Use migration naming conventions and coordinate via git
- **RISK-004**: Performance issues with default EF Core query generation - Mitigation: Review generated SQL and optimize where needed
- **ASSUMPTION-004**: PostgreSQL connection string is configured in appsettings.Development.json
- **ASSUMPTION-005**: pgvector extension is already installed in PostgreSQL container from Phase 1

## 8. Success Metrics

- **METRIC-002**: All TEST-XXX tests passing (exit code 0)
- **METRIC-003**: Build successful with no errors (exit code 0)
- **METRIC-007**: All EF Core migrations apply successfully without errors
- **METRIC-008**: Database schema matches entity model (no pending migrations)
- **METRIC-009**: All foreign key relationships enforced correctly
- **METRIC-010**: Query execution plans show index usage on filtered queries

## 9. Related Specifications / Further Reading

- [Entity Framework Core Documentation](https://docs.microsoft.com/en-us/ef/core/)
- [PostgreSQL Schema Design Best Practices](https://www.postgresql.org/docs/current/ddl.html)
- [ASP.NET Core Identity Documentation](https://docs.microsoft.com/en-us/aspnet/core/security/authentication/identity)
