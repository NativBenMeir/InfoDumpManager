---
goal: AI Agents Architecture Implementation Plan
version: 1.0
date_created: 2026-02-01
last_updated: 2026-02-01
owner: InfoDumpManager Team
status: 'Planned'
tags: [design, architecture, feature, ai, agents]
---

# Introduction

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

This plan defines a deterministic, executable roadmap to implement the multi-agent AI processing architecture described in .DesignDocs/AIAgentsArchitecture.md, including agents, orchestrator, job queue, background processing, provider abstractions, and persistence integrations.

## 1. Requirements & Constraints

- **REQ-001**: Implement agent contracts and capabilities as described in .DesignDocs/AIAgentsArchitecture.md.
- **REQ-002**: Implement multi-agent orchestration with summarization → categorization → tagging → validation pipeline.
- **REQ-003**: Provide background processing with retry and job queue semantics.
- **REQ-004**: Add provider abstractions for LLM and embeddings with resilience.
- **REQ-005**: Persist summaries, tags, and embeddings in existing persistence layer.
- **SEC-001**: Ensure cost management and budget enforcement hooks exist for every LLM/embedding call.
- **CON-001**: Target .NET 8 with nullable reference types enabled.
- **CON-002**: Follow clean architecture layering (Domain/Application/Infrastructure/Presentation).
- **GUD-001**: Use async/await and name async methods with Async suffix.
- **PAT-001**: Coordinator pattern for the orchestrator with agent interfaces in Application and implementations in Infrastructure.

## 2. Implementation Steps

### Implementation Phase 1

- GOAL-001: Define contracts and domain events for the AI agents architecture.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Add agent contracts and result models in Application layer |  |  |
| TASK-002 | Add orchestration contracts and processing models in Application layer |  |  |
| TASK-003 | Add domain events for AI processing lifecycle in Domain layer |  |  |

### Implementation Phase 2

- GOAL-002: Implement orchestration, job queue, and background processing services.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-004 | Implement in-memory job queue and processing job model |  |  |
| TASK-005 | Implement orchestrator and pipeline execution flow |  |  |
| TASK-006 | Implement background service that drains queue and retries |  |  |

### Implementation Phase 3

- GOAL-003: Implement provider abstractions, agents, and resilience.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-007 | Add LLM provider abstraction and Semantic Kernel adapter with Polly |  |  |
| TASK-008 | Add embedding provider abstraction, cache, and pgvector store |  |  |
| TASK-009 | Implement Summarization, Categorization, Tagging, Validation agents |  |  |
| TASK-010 | Implement cost management service and usage tracking |  |  |

### Implementation Phase 4

- GOAL-004: Wire integrations and add API/UI hooks for monitoring.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-011 | Register services in WebAPI and Web DI containers |  |  |
| TASK-012 | Add API endpoints for processing triggers and job status |  |  |
| TASK-013 | Add telemetry/logging for agent execution metrics |  |  |

### Implementation Phase 5

- GOAL-005: Add tests and validation of the AI agents pipeline.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-014 | Add unit tests for agents and orchestrator |  |  |
| TASK-015 | Add integration tests for background processing and storage |  |  |
| TASK-016 | Add performance benchmarks for batch processing |  |  |

## 3. Task Details

- **TASK-001**: Create Application contracts in:
  - src/InfoDumpManager.Application/Agents/IAgent.cs
  - src/InfoDumpManager.Application/Agents/AgentModels.cs
  - Define `IAgent`, `AgentCapability`, `AgentContext`, `AgentResult`, `AgentMetrics`, `AgentResultConfidence`.
- **TASK-002**: Create orchestration contracts in:
  - src/InfoDumpManager.Application/Agents/Orchestration/IContentProcessingOrchestrator.cs
  - Include `ProcessingResult`, `ProcessingStatus`, `ProcessingOptions`.
- **TASK-003**: Add domain events in:
  - src/InfoDumpManager.Domain/Events/GEMProcessingEvents.cs
  - Define `GEMCreatedAndQueuedForProcessing`, `GEMSummarizationStarted`, `GEMSummarizationCompleted`, `GEMCategorizationSuggested`, `GEMProcessingCompleted`, `GEMProcessingFailed`, `CategorySuggestionRejectedByUser`.

- **TASK-004**: Implement job queue in:
  - src/InfoDumpManager.Application/Infrastructure/JobQueue/IJobQueue.cs
  - src/InfoDumpManager.Application/Infrastructure/JobQueue/InMemoryJobQueue.cs
  - src/InfoDumpManager.Application/Infrastructure/JobQueue/ProcessingJob.cs
  - Provide `EnqueueAsync`, `DequeueAsync`, `MarkCompleteAsync`, `MarkFailedAsync`, `DequeueBatchAsync`.
- **TASK-005**: Implement orchestrator in:
  - src/InfoDumpManager.Application/Agents/Orchestration/ContentProcessingOrchestrator.cs
  - Implement `ProcessGEMAsync`, `ProcessBatchAsync`, `GetJobStatusAsync`, `WatchJobAsync`.
  - Orchestrate agent calls in order, update progress, persist outputs via repositories.
- **TASK-006**: Implement background service in:
  - src/InfoDumpManager.Application/Services/ContentProcessingBackgroundService.cs
  - Use `BackgroundService` to drain queue, call orchestrator, handle retries.

- **TASK-007**: Add LLM provider abstraction in:
  - src/InfoDumpManager.Application/Services/LLM/ILLMProvider.cs
  - src/InfoDumpManager.Application/Services/LLM/LLMResponse.cs
  - Implement adapter in Infrastructure:
    - src/InfoDumpManager.Infrastructure/Services/LLM/SemanticKernelProvider.cs
  - Add Polly retry and circuit breaker policies at provider layer.
- **TASK-008**: Add embedding abstractions and storage:
  - src/InfoDumpManager.Application/Services/Embeddings/IEmbeddingProvider.cs
  - src/InfoDumpManager.Application/Services/Embeddings/IVectorStore.cs
  - src/InfoDumpManager.Infrastructure/Services/Embeddings/RedisEmbeddingCache.cs
  - src/InfoDumpManager.Infrastructure/Services/Embeddings/PostgreSqlVectorStore.cs
  - Ensure pgvector mapping is configured in Infrastructure DbContext.
- **TASK-009**: Implement agents in:
  - src/InfoDumpManager.Application/Agents/Implementations/SummarizationAgent.cs
  - src/InfoDumpManager.Application/Agents/Implementations/CategorizationAgent.cs
  - src/InfoDumpManager.Application/Agents/Implementations/TaggingAgent.cs
  - src/InfoDumpManager.Application/Agents/Implementations/ValidationAgent.cs
  - Each agent implements `IAgent` and exposes specialized methods as needed.
- **TASK-010**: Implement cost management in:
  - src/InfoDumpManager.Application/Services/CostManagement/ICostManager.cs
  - src/InfoDumpManager.Application/Services/CostManagement/CostManagerImpl.cs
  - Add repositories in Infrastructure for cost usage persistence.

- **TASK-011**: Register services in:
  - src/InfoDumpManager.WebAPI/Program.cs
  - src/InfoDumpManager.Web/Program.cs
  - Register agents, orchestrator, job queue, providers, and background service.
- **TASK-012**: Add API endpoints in:
  - src/InfoDumpManager.WebAPI/Controllers/AiProcessingController.cs
  - Endpoints: `POST /api/ai/process`, `GET /api/ai/jobs/{jobId}`.
- **TASK-013**: Add structured logging in:
  - src/InfoDumpManager.Application/Agents/Implementations/*Agent.cs
  - Emit metrics: tokens, cost, duration, retries.

- **TASK-014**: Add unit tests in:
  - tests/InfoDumpManager.Tests.Unit/AIAgents/AgentContractsTests.cs
  - tests/InfoDumpManager.Tests.Unit/AIAgents/OrchestratorTests.cs
  - tests/InfoDumpManager.Tests.Unit/AIAgents/CostManagerTests.cs
- **TASK-015**: Add integration tests in:
  - tests/InfoDumpManager.Tests.Integration/AIAgentsProcessingIntegrationTests.cs
  - Validate queue processing, persistence, and pgvector storage.
- **TASK-016**: Add performance benchmarks in:
  - tests/InfoDumpManager.Tests.Integration/PerformanceBenchmarkTests.cs
  - Add scenarios for batch processing with concurrency limits.

## 4. Alternatives

- **ALT-001**: Implement agents directly in WebAPI project; rejected due to clean architecture violations.
- **ALT-002**: Use a single monolithic AI service; rejected to preserve extensibility and testability.

## 5. Dependencies

- **DEP-001**: Microsoft.SemanticKernel (LLM orchestration)
- **DEP-002**: Polly (resilience policies)
- **DEP-003**: MediatR (domain event publishing)
- **DEP-004**: pgvector + Npgsql.EntityFrameworkCore.PostgreSQL
- **DEP-005**: StackExchange.Redis (embedding cache)

## 6. Files

- **FILE-001**: src/InfoDumpManager.Application/Agents/IAgent.cs
- **FILE-002**: src/InfoDumpManager.Application/Agents/AgentModels.cs
- **FILE-003**: src/InfoDumpManager.Application/Agents/Orchestration/ContentProcessingOrchestrator.cs
- **FILE-004**: src/InfoDumpManager.Application/Infrastructure/JobQueue/InMemoryJobQueue.cs
- **FILE-005**: src/InfoDumpManager.Application/Services/ContentProcessingBackgroundService.cs
- **FILE-006**: src/InfoDumpManager.Infrastructure/Services/LLM/SemanticKernelProvider.cs
- **FILE-007**: src/InfoDumpManager.Infrastructure/Services/Embeddings/PostgreSqlVectorStore.cs
- **FILE-008**: src/InfoDumpManager.WebAPI/Controllers/AiProcessingController.cs
- **FILE-009**: tests/InfoDumpManager.Tests.Unit/AIAgents/OrchestratorTests.cs

## 7. Testing

- **TEST-001**: Verify `ContentProcessingOrchestrator.ProcessGEMAsync` returns `Completed` for successful agent pipeline.
- **TEST-002**: Verify retry behavior in `InMemoryJobQueue.MarkFailedAsync` with exponential backoff.
- **TEST-003**: Verify embedding storage and similarity search with pgvector.
- **TEST-004**: Verify cost budget denial returns failure without provider calls.
- **TEST-005**: Integration test end-to-end processing via `POST /api/ai/process`.

## 8. Risks & Assumptions

- **RISK-001**: External LLM API failures increase latency; mitigate with Polly and fallback logic.
- **RISK-002**: pgvector performance may degrade with large embeddings; mitigate with indexing and batching.
- **ASSUMPTION-001**: Required provider keys and configuration values are available via environment variables.
- **ASSUMPTION-002**: Existing repositories and entities can be extended without schema breaking changes.

## 9. Related Specifications / Further Reading

[.DesignDocs/AIAgentsArchitecture.md](.DesignDocs/AIAgentsArchitecture.md)
[docs/api.md](docs/api.md)