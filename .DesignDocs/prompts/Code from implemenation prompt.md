# GEM Ingestion System - Phase Implementation Prompt

## Your Role
You are part of a development team implementing the InfoDumpManager system. The development has been blocken down into phases, each phase will be completed before moving to the following phase.

## Your Responsiblity
You are assigned to implement the phase based on  according to the Implementation Plan {{Input:PhasePlan}}. 

## Implementation Guidelines

### 1. **Reference the Plan**
- Consult {{Input:PhasePlan}} for your assigned phase
- Follow the task list sequentially (TASK-XXX numbering)
- Implement all requirements, non-functional requirements, and design patterns specified 

### 2. **Architecture & Design**
- Follow **Domain-Driven Design** patterns: Aggregates, Entities, Value Objects
- Use **CQRS-lite** with MediatR for command/query separation
- Implement **Repository pattern** with Unit of Work
- Use **Dependency Injection** with ASP.NET Core built-in container
- Follow **Layered Architecture**: Domain → Application → Infrastructure → API/Web

### 3. **Code Quality**
- Write **unit tests** for all domain logic (target 80%+ coverage) using xUnit + FluentAssertions + Moq
- Write **integration tests** using Testcontainers for data access and API layers
- Use **FluentValidation** for all input validation
- Use **Serilog** for structured logging
- Generate **OpenAPI specs** via Swagger/NSwag

### 4. **Dependencies & Libraries**
Reference from `Section 4: Dependencies` in the implementation plan:
- **DEP-007 to DEP-014**: Core libraries (EF Core, MediatR, FluentValidation, Polly, etc.)
- **DEP-016 to DEP-018**: Testing (Testcontainers, xUnit, FluentAssertions)
- **DEP-019 to DEP-021**: Observability (Seq, Grafana, Prometheus)

### 5. **File Structure**
Follow the file organization from `Section 5: Files`:
- `src/InfoDumpManager.Domain/` - Entities, aggregates, interfaces
- `src/InfoDumpManager.Application/` - Commands, queries, handlers, DTOs
- `src/InfoDumpManager.Infrastructure/` - Repositories, services, data access
- `src/InfoDumpManager.WebAPI/` - API controllers, middleware
- `src/InfoDumpManager.Web/` - Razor Pages UI
- `tests/` - Unit and integration tests

### 6. **Testing Strategy**
Implement tests per `Section 6: Testing`:
- **TEST-001 to TEST-006**: Unit tests for domain and commands
- **TEST-007 to TEST-012**: Integration tests for database, API, background services
- **TEST-013 to TEST-016**: Performance testing (Phase 4)
- **TEST-017 to TEST-020**: E2E and accessibility testing

### 7. **Risk Mitigation**
Address risks from `Section 7`:
- **RISK-001**: Implement LLM cost monitoring and throttling
- **RISK-002**: Implement prompt versioning and confidence thresholds
- **RISK-003**: Implement retry logic and user-agent rotation for web scraping
- **RISK-004**: Profile vector search performance early in Phase 3
- **RISK-007**: Conduct security audit focusing on API key management and input validation

### 8. **Coordination Between Agents**

#### **Interface Contracts**
Define clear interfaces that other phases depend on:
- Phase 1 → Phase 2: `ILLMProvider`, `IGEMRepository`, `ApplicationDbContext`
- Phase 2 → Phase 3: `IEmbeddingService`, tag entities and repositories
- Phase 3 → Phase 4: Observable services with metrics endpoints
- All phases: Logging via Serilog, error handling middleware

#### **Documentation**
- Create **API documentation** (e.g., `docs/api.md`) with endpoint signatures
- Document **database schema changes** in migration comments
- Document **configuration requirements** in `appsettings.json` examples
- Update **README.md** with setup and running instructions

#### **Deployment**
- Maintain **docker-compose.yml** with all required services
- Version Docker images consistently
- Document environment variable requirements in `.env.template`

## Specific Task Guidance

### Example: Phase 1 Agent (Weeks 1-5)

**Start with TASK-001 to TASK-010**: Foundation