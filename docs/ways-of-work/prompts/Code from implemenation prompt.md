# GEM Ingestion System - Multi-Agent Implementation Prompt

## Overview
You are part of a development team implementing the GEM (Generated Enriched Memory) Ingestion, Summarization, and Smart Categorization system according to the Implementation Plan v1.0 located at:
`docs/ways-of-work/plan/gem-ingestion-categorization/implementation-plan-1.md`

The project is being built with .NET 8.0, ASP.NET Core, PostgreSQL with pgvector, Docker, and AI integration with OpenAI/Azure OpenAI.

## Your Role
You are assigned to implement **Phase [X]** of the 4-phase delivery plan. Multiple agents are working in parallel on different phases and components. Coordinate through clear documentation and well-structured code.

## Implementation Phases (Choose One)

### **Agent 1: Phase 1 - Foundation & Basic Ingestion (TASK-001 to TASK-030)**
- Set up .NET 8 solution structure and Docker Compose environment
- Design domain model (GEM, Category, User, ActivityLog aggregates)
- Implement PostgreSQL schema and repositories
- Build basic Web API and Razor Pages UI
- Implement web scraping service and manual categorization

**Key Deliverable**: Working solution with URL ingestion, manual categorization, and basic API

### **Agent 2: Phase 2 - AI Summarization & Auto-Categorization (TASK-031 to TASK-060)**
- Build LLM provider abstraction layer (OpenAI, Azure OpenAI)
- Implement background job processing infrastructure
- Create AI summarization and categorization services
- Add UI for viewing summaries and accepting AI suggestions
- Integrate metrics and error handling

**Key Deliverable**: Fully functional AI-powered summarization and categorization with confidence thresholds

### **Agent 3: Phase 3 - Tagging, Search & Q&A Synthesis (TASK-061 to TASK-095)**
- Design Tag entity and pgvector embedding columns
- Implement semantic embedding generation and vector search
- Create AI tagging service with tag suggestion
- Build hybrid search (full-text + semantic)
- Implement RAG-based Q&A and category synthesis endpoints

**Key Deliverable**: Complete search, tagging, and Q&A capabilities with semantic understanding

### **Agent 4: Phase 4 - Polish, Observability & Production Readiness (TASK-096 to TASK-128)**
- Implement centralized logging (Serilog, Seq/ELK)
- Set up distributed tracing (OpenTelemetry)
- Configure Prometheus metrics and Grafana dashboards
- Optimize database queries and implement caching
- Create operational runbooks and production Docker Compose

**Key Deliverable**: Production-ready system with full observability, security hardening, and load testing validation

## Implementation Guidelines

### 1. **Reference the Plan**
- Consult `implementation-plan-1.md` for your assigned phase
- Follow the task list sequentially (TASK-XXX numbering)
- Implement all requirements, non-functional requirements, and design patterns specified in Section 1

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