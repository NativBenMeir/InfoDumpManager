---
goal: Implementation Plan for GEM Ingestion, Summarization, and Smart Categorization System
phase_title: Authentication & API Foundation
PhaseNumber: 4
version: 1.1
date_created: 2026-01-28
last_updated: 2026-01-28
tags: [authentication, api, security, jwt, aspnetcore]
depends_on: [1, 2, 3]
status: Planned
status_color: blue
---

# Introduction

![Status: Planned](https://img.shields.io/badge/Status-Planned-blue)

This phase implements authentication and authorization infrastructure using ASP.NET Core Identity with JWT bearer tokens. It creates the foundational API controllers for GEMs and Categories, implements MediatR command/query handlers following CQRS-lite pattern, and establishes FluentValidation for input validation. The phase also sets up error handling middleware and comprehensive API documentation with Swagger.

## 1. Requirements & Constraints

- **REQ-005**: System must provide manual category management (create, rename, merge, delete, reassign)
- **CON-001**: Must use .NET 8.0 LTS as primary framework
- **CON-003**: Must use ASP.NET Core for all web applications and APIs
- **CON-004**: Must follow domain-driven design with clear layer separation
- **CON-005**: Must support both self-hosted (Docker Compose) and future SaaS (K8s-ready) deployment
- **NFR-002**: System must be designed for multi-tenant SaaS scalability from day one
- **NFR-003**: All data must be encrypted at rest and in transit
- **NFR-004**: System must provide comprehensive observability (logging, metrics, tracing)
- **SEC-001**: Implement ASP.NET Core Identity for authentication and user management
- **SEC-002**: Use JWT bearer tokens for API authentication
- **SEC-003**: Implement claims-based authorization with multi-tenancy support
- **SEC-004**: Ensure row-level security for multi-tenant data isolation
- **SEC-005**: Store all secrets in environment variables or secure vaults (not in code)
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
| TASK-011 | Set up ASP.NET Core Identity with user registration and login endpoints | | |
| TASK-012 | Implement JWT bearer token authentication for API access | | |
| TASK-013 | Create GEM API Controller with endpoints: POST /api/v1/gems (create), GET /api/v1/gems/{id}, GET /api/v1/gems (list with pagination) | | |
| TASK-014 | Create Category API Controller with endpoints: POST /api/v1/categories, GET /api/v1/categories, PUT /api/v1/categories/{id}, DELETE /api/v1/categories/{id} | | |
| TASK-017 | Create MediatR 14.0.0 command handlers: CreateGEMCommand, AssignCategoryCommand, CreateCategoryCommand | | |
| TASK-018 | Implement FluentValidation 12.1.1 validators for all commands and DTOs | | |
| TASK-028 | Implement basic error handling middleware with structured error responses | | |
| TASK-038-P4 | Configure claims-based authorization policies for multi-tenancy support | | |
| TASK-TST-P4 | Implement all tests based on per Testing section in this plan. |  |  |

## 3. Alternatives

- **ALT-008**: GraphQL API Instead of REST - Rejected to reduce complexity for current requirements
- **ALT-004**: Blazor WebAssembly SPA Instead of Razor Pages + HTMX - Rejected for simpler architecture and better SEO

## 4. Dependencies

- **PHASE-DEP-003**: Requires domain model and repositories from Phase 3 - Verify repository interfaces are implemented
- **DEP-012**: MediatR 14.0.0 - CQRS pattern library
- **DEP-013**: FluentValidation 12.1.1 - Input validation library
- **SEC-001**: ASP.NET Core Identity for authentication

## 5. Files

- **FILE-044**: `src/InfoDumpManager.WebAPI/Controllers/GEMsController.cs` - GEM API endpoints
- **FILE-045**: `src/InfoDumpManager.WebAPI/Controllers/CategoriesController.cs` - Category API endpoints
- **FILE-045-P4**: `src/InfoDumpManager.WebAPI/Controllers/AuthController.cs` - Authentication endpoints
- **FILE-022**: `src/InfoDumpManager.Application/GEMs/Commands/CreateGEMCommand.cs` - MediatR command for creating GEM
- **FILE-023**: `src/InfoDumpManager.Application/GEMs/Commands/CreateGEMCommandHandler.cs` - Handler for CreateGEMCommand
- **FILE-024**: `src/InfoDumpManager.Application/GEMs/Commands/AssignCategoryCommand.cs` - Command for assigning category to GEM
- **FILE-027**: `src/InfoDumpManager.Application/Categories/Commands/CreateCategoryCommand.cs` - Command for creating category
- **FILE-029**: `src/InfoDumpManager.Application/GEMs/DTOs/GEMDto.cs` - Data transfer object for GEM
- **FILE-030**: `src/InfoDumpManager.Application/GEMs/Validators/CreateGEMCommandValidator.cs` - FluentValidation validator
- **FILE-049**: `src/InfoDumpManager.WebAPI/Middleware/ErrorHandlingMiddleware.cs` - Global error handling

## 6. Testing

- **TEST-021**: Integration Test - Authentication - User registration - Expected: User created with 201 status
- **TEST-022**: Integration Test - Authentication - User login with valid credentials - Expected: JWT token returned
- **TEST-023**: Integration Test - Authentication - User login with invalid credentials - Expected: 401 Unauthorized
- **TEST-024**: Integration Test - GEM API - Create GEM with valid token - Expected: 201 Created with GEM ID
- **TEST-025**: Integration Test - GEM API - Create GEM without token - Expected: 401 Unauthorized
- **TEST-026**: Integration Test - GEM API - Get GEM by ID - Expected: 200 OK with GEM data
- **TEST-027**: Integration Test - Category API - Create category - Expected: 201 Created
- **TEST-028**: Unit Test - Validation - CreateGEMCommand with invalid URL - Expected: Validation error
- **TEST-029**: Integration Test - Error Handling - API throws exception - Expected: 500 with structured error response

### Test Requirements
- All API endpoints must have integration tests with Testcontainers
- Authentication flows must be tested (registration, login, token validation)
- Authorization must be tested (authenticated vs unauthenticated requests)
- All validators must have unit tests covering valid and invalid inputs

## 7. Risks & Assumptions

- **RISK-007**: JWT token secrets must be securely managed - Mitigation: Use environment variables and never commit secrets to git
- **RISK-008**: Token expiration and refresh strategy not implemented in Phase 4 - Mitigation: Plan for refresh token implementation in future phase
- **ASSUMPTION-008**: JWT tokens have reasonable expiration time (1 hour for access tokens)
- **ASSUMPTION-009**: Multi-tenancy is enforced via claims in JWT tokens

## 8. Success Metrics

- **METRIC-002**: All TEST-XXX tests passing (exit code 0)
- **METRIC-003**: Build successful with no errors (exit code 0)
- **METRIC-014**: All API endpoints return proper HTTP status codes (2xx, 4xx, 5xx)
- **METRIC-015**: Swagger UI displays all endpoints with request/response examples
- **METRIC-016**: Authentication flow completes successfully with valid JWT token
- **METRIC-017**: Invalid requests return structured error responses with validation details

## 9. Related Specifications / Further Reading

- [ASP.NET Core Identity Documentation](https://docs.microsoft.com/en-us/aspnet/core/security/authentication/identity)
- [JWT Bearer Authentication](https://jwt.io/introduction)
- [MediatR Documentation](https://github.com/jbogard/MediatR)
- [FluentValidation Documentation](https://docs.fluentvalidation.net/)
