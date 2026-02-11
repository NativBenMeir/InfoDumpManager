# Code Review Implementation Plan — Master Index

**Source Review:** `.DesignDocs/CodeReviews/Opus46_review_2026_02_10.md`
**Created:** February 10, 2026
**Author:** GitHub Copilot

---

## Overview

This plan implements the 10 recommendations from the architecture review in 5 sequential phases. Each phase is self-contained: it produces a building, testable solution before the next phase begins.

## Dependency Order

```
Phase 1 ──► Phase 2 ──► Phase 3 ──► Phase 4 ──► Phase 5
Domain       Infra       App         DI/API      Tests
```

Each phase depends on the previous one. Do not skip phases.

---

## Phase Summary

| Phase | File | Focus | Key Changes |
|-------|------|-------|-------------|
| 1 | [Phase1-DomainLayerFixes.md](Phase1-DomainLayerFixes.md) | Domain layer | Add domain events to `AggregateRoot`, move `User` to Infrastructure, expose shared validation constants |
| 2 | [Phase2-InfrastructureLayerFixes.md](Phase2-InfrastructureLayerFixes.md) | Infrastructure layer | EF Core domain event interceptor, fix `UnitOfWork` DI split, move `IStorageService` to Application |
| 3 | [Phase3-ApplicationLayerFixes.md](Phase3-ApplicationLayerFixes.md) | Application layer | MediatR `ValidationBehavior`, remove duplicate validators, extract `IJobTracker`, move `BackgroundService` |
| 4 | [Phase4-DICentralization-PresentationFixes.md](Phase4-DICentralization-PresentationFixes.md) | DI & Presentation | Shared `AddApplication()`/`AddInfrastructure()` extensions, remove `Console.WriteLine`, fix `CurrentUserContext` |
| 5 | [Phase5-TestUpdates-Verification.md](Phase5-TestUpdates-Verification.md) | Tests & verification | Fix broken tests, add tests for new components, full verification |

---

## Review Recommendations ↔ Phase Mapping

| # | Recommendation | Phase |
|---|---------------|-------|
| 1 | Remove ASP.NET dependency from Domain | Phase 1 (1.2) |
| 2 | Add domain events to AggregateRoot | Phase 1 (1.1) + Phase 2 (2.1) |
| 3 | Break up ContentProcessingOrchestrator | Phase 3 (3.3) |
| 4 | Fix UnitOfWork / repository DI split | Phase 2 (2.2) |
| 5 | Deduplicate service registration | Phase 4 (4.1–4.3) |
| 6 | Deduplicate validation + add ValidationBehavior | Phase 1 (1.3) + Phase 3 (3.1–3.2) |
| 7 | Replace InMemoryJobQueue with durable store | *Deferred* (see below) |
| 8 | Move BackgroundService out of Application | Phase 3 (3.4) |
| 9 | Centralize DI registration | Phase 4 (4.1–4.3) |
| 10 | Remove Console.WriteLine from pipeline | Phase 4 (4.2) |

---

## Deferred Items

The following items from the review are **not included** in this plan because they require new technology decisions or significant new infrastructure:

1. **Replace InMemoryJobQueue with a durable store** — Requires choosing between Redis Streams, PostgreSQL-backed queue, or a message broker (RabbitMQ/Azure Service Bus). This should be a separate design doc and implementation effort.

2. **Configure SemanticKernel with real AI services** — Requires OpenAI/Azure OpenAI API keys and configuration. The empty `Kernel` is a known placeholder for Phase 3+ of the project.

3. **Centralize Polly resilience pipelines** — The current Polly v7-style policies work. Migration to Polly v8 `ResiliencePipeline` is a low-priority refactor that can happen independently.

4. **Remove SummarizationAgent dependency on IGEMRepository** — Requires changing the `AgentContext` to carry more data (e.g., the full snapshot content). This is a behavior change that needs separate testing.

---

## Execution Instructions for AI Agent

1. Read the phase document before starting.
2. Make all code changes described in the phase.
3. After each phase, run `dotnet build`. Fix any compilation errors before proceeding.
4. After Phase 5, run `dotnet test`. Fix any test failures.
5. Do not modify test assertions unless the test is testing moved/renamed types.
6. Preserve all existing behavior — these are structural/organizational changes, not feature changes.

---

## Files Created/Modified/Deleted Summary

### New Files
- `src/InfoDumpManager.Domain/Entities/UserProfile.cs`
- `src/InfoDumpManager.Domain/Common/IAggregateWithEvents.cs`
- `src/InfoDumpManager.Infrastructure/Data/DomainEventDispatchInterceptor.cs`
- `src/InfoDumpManager.Application/Common/Behaviors/ValidationBehavior.cs`
- `src/InfoDumpManager.Application/Agents/Orchestration/IJobTracker.cs`
- `src/InfoDumpManager.Application/Agents/Orchestration/InMemoryJobTracker.cs`
- `src/InfoDumpManager.Application/Services/Storage/IStorageService.cs`
- `src/InfoDumpManager.Application/DependencyInjection.cs`
- `src/InfoDumpManager.Infrastructure/DependencyInjection.cs`
- `tests/InfoDumpManager.Tests.Unit/Common/ValidationBehaviorTests.cs`
- `tests/InfoDumpManager.Tests.Unit/Common/AggregateRootDomainEventsTests.cs`
- `tests/InfoDumpManager.Tests.Unit/AIAgents/JobTrackerTests.cs`

### Moved Files
- `src/InfoDumpManager.Domain/Entities/User.cs` → `src/InfoDumpManager.Infrastructure/Data/Entities/User.cs`
- `src/InfoDumpManager.Application/Services/ContentProcessingBackgroundService.cs` → `src/InfoDumpManager.Infrastructure/Services/ContentProcessingBackgroundService.cs`
- `src/InfoDumpManager.Application/Infrastructure/JobQueue/InMemoryJobQueue.cs` → `src/InfoDumpManager.Infrastructure/Services/InMemoryJobQueue.cs`

### Deleted Files
- `src/InfoDumpManager.Infrastructure/Services/IStorageService.cs` (moved to Application)
- `src/InfoDumpManager.WebAPI/Validators/GEMs/CreateGemRequestValidator.cs` (duplicate)
- `src/InfoDumpManager.WebAPI/Validators/GEMs/AssignCategoryRequestValidator.cs` (duplicate, if applicable)
- `src/InfoDumpManager.WebAPI/Validators/Categories/CreateCategoryRequestValidator.cs` (duplicate, if applicable)

### Significantly Modified Files
- `src/InfoDumpManager.Domain/Common/AggregateRoot.cs`
- `src/InfoDumpManager.Domain/InfoDumpManager.Domain.csproj`
- `src/InfoDumpManager.Domain/Entities/GEM.cs` (constants visibility)
- `src/InfoDumpManager.Domain/Entities/Category.cs` (constants visibility)
- `src/InfoDumpManager.Domain/Entities/Tag.cs` (constants visibility)
- `src/InfoDumpManager.Infrastructure/InfoDumpManager.Infrastructure.csproj`
- `src/InfoDumpManager.Infrastructure/Repositories/UnitOfWork.cs`
- `src/InfoDumpManager.Infrastructure/Data/ApplicationDbContext.cs`
- `src/InfoDumpManager.Infrastructure/Services/MinioStorageService.cs`
- `src/InfoDumpManager.Application/Validators/CreateGEMCommandValidator.cs`
- `src/InfoDumpManager.Application/Agents/Orchestration/ContentProcessingOrchestrator.cs`
- `src/InfoDumpManager.WebAPI/Program.cs`
- `src/InfoDumpManager.WebAPI/Middleware/ErrorHandlingMiddleware.cs`
- `src/InfoDumpManager.WebAPI/Services/CurrentUserContext.cs`
- `src/InfoDumpManager.Web/Program.cs`
- Multiple test files (using statement updates)
