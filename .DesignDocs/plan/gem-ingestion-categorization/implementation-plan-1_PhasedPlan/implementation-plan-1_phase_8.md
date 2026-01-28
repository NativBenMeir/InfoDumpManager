---
goal: Implementation Plan for GEM Ingestion, Summarization, and Smart Categorization System
phase_title: AI Summarization & Background Job Processing
PhaseNumber: 8
version: 1.1
date_created: 2026-01-28
last_updated: 2026-01-28
tags: [ai, summarization, background-jobs, async, prompts]
depends_on: [1, 2, 3, 4, 5, 7]
status: Planned
status_color: blue
---

# Introduction

![Status: Planned](https://img.shields.io/badge/Status-Planned-blue)

This phase implements AI-powered summarization with background job processing infrastructure. It creates background services using IHostedService pattern, designs and implements prompt templates for summarization, and integrates with the LLM providers established in Phase 7. The phase delivers automatic summary generation for all ingested GEMs with job status tracking and UI updates.

## 1. Requirements & Constraints

- **REQ-002**: System must generate AI-powered summaries for all ingested content
- **CON-001**: Must use .NET 10.0.2 LTS as primary framework
- **CON-004**: Must follow domain-driven design with clear layer separation
- **CON-005**: Must support both self-hosted (Docker Compose) and future SaaS (K8s-ready) deployment
- **CON-007**: All background processing must use IHostedService/BackgroundService patterns
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
| TASK-035 | Design and implement prompt templates for summarization (system prompt + user content template) with version tracking | | |
| TASK-039 | Set up background job queue infrastructure using System.Threading.Channels for producer-consumer pattern | | |
| TASK-040 | Implement AI Summarization Background Service (inherits BackgroundService) that processes GEMs from queue | | |
| TASK-042 | Add GEMSummary generation to summarization service with fields: summary text, generated timestamp, model used, token count | | |
| TASK-046 | Implement job status tracking entity (JobStatus table) with fields: job_id, job_type, status, created_at, completed_at, error_message | | |
| TASK-047 | Modify CreateGEMCommand handler to enqueue summarization job after saving GEM | | |
| TASK-048 | Implement webhook or polling mechanism to notify Web UI when summarization completes (SignalR or simple polling) | | |
| TASK-059 | Document prompt engineering decisions and version history in docs/prompts/ directory | | |

## 3. Alternatives

- **ALT-005**: Hangfire for Background Jobs Instead of IHostedService - Deferred to future phases. IHostedService is lightweight and sufficient
- **ALT-003**: RabbitMQ or Azure Service Bus for Job Queue Instead of In-Memory Channels - Deferred for self-hosted simplicity

## 4. Dependencies

- **PHASE-DEP-009**: Requires LLM providers from Phase 7 - Verify ILLMProvider implementations are functional
- **PHASE-DEP-010**: Requires GEM creation from Phase 5 - Verify CreateGEMCommand handler exists
- **DEP-001**: LLM API Provider (OpenAI or Azure OpenAI) - Critical for summarization
- **DEP-009**: Microsoft.SemanticKernel 1.70.0 - For prompt management
- **DEP-010**: Serilog 4.3.0 - Structured logging framework

## 5. Files

- **FILE-041**: `src/InfoDumpManager.Infrastructure/BackgroundServices/SummarizationBackgroundService.cs` - Summarization worker
- **FILE-041-P8**: `src/InfoDumpManager.Infrastructure/BackgroundServices/JobQueue.cs` - Job queue implementation
- **FILE-041-P8**: `src/InfoDumpManager.Infrastructure/BackgroundServices/JobStatusTracker.cs` - Job status tracking service
- **FILE-039**: `src/InfoDumpManager.Infrastructure/Services/LLMOrchestrationService.cs` - LLM orchestration
- **FILE-039-P8**: `src/InfoDumpManager.Infrastructure/Prompts/SummarizationPrompt.txt` - Summarization prompt template
- **FILE-012**: `src/InfoDumpManager.Domain/ValueObjects/GEMSummary.cs` - AI-generated summary value object
- **FILE-044-P8**: `src/InfoDumpManager.WebAPI/Hubs/JobStatusHub.cs` - SignalR hub for job status updates

## 6. Testing

- **TEST-056**: Unit Test - Summarization Prompt - Format prompt with GEM content - Expected: Proper prompt structure
- **TEST-057**: Integration Test - Summarization Service - Process GEM through queue - Expected: Summary generated and saved
- **TEST-058**: Integration Test - Job Status - Track job lifecycle - Expected: Status transitions from Pending → Processing → Completed
- **TEST-059**: Integration Test - Background Service - Start and stop gracefully - Expected: No data loss on shutdown
- **TEST-060**: Unit Test - Mock LLM - Summarization with mock response - Expected: Deterministic summary output
- **TEST-061**: Integration Test - End-to-End - Create GEM → auto summarize - Expected: Summary appears in database
- **TEST-062**: Performance Test - Summarization - P95 latency < 15 seconds - Expected: Meets NFR-001
- **TEST-063**: Integration Test - SignalR - Job completion notification - Expected: UI receives update event

### Test Requirements
- Background services must handle graceful shutdown without data loss
- Job queue must be tested under concurrent load
- Summary quality must be validated with sample content
- SignalR notifications must reach connected clients

## 7. Risks & Assumptions

- **RISK-017**: LLM API latency may exceed 15 second target - Mitigation: Optimize prompts and use faster models
- **RISK-018**: Background service crashes may lose queued jobs - Mitigation: Implement persistent queue in future phase
- **RISK-019**: Prompt quality directly impacts summary usefulness - Mitigation: Iterate on prompts with user feedback
- **ASSUMPTION-016**: In-memory queue is sufficient for self-hosted deployment with single instance
- **ASSUMPTION-017**: Typical web pages generate summaries within token limits (e.g., 4000 tokens input)

## 8. Success Metrics

- **METRIC-002**: All TEST-XXX tests passing (exit code 0)
- **METRIC-003**: Build successful with no errors (exit code 0)
- **METRIC-030**: P95 summarization latency < 15 seconds (NFR-001)
- **METRIC-031**: Background service processes jobs without memory leaks over 24 hours
- **METRIC-032**: Job success rate > 95% (excluding invalid content)
- **METRIC-033**: Summary quality assessed as useful by manual review of 20 samples

## 9. Related Specifications / Further Reading

- [.NET Background Services Documentation](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services)
- [System.Threading.Channels](https://devblogs.microsoft.com/dotnet/an-introduction-to-system-threading-channels/)
- [Prompt Engineering Guide](https://platform.openai.com/docs/guides/prompt-engineering)
- [SignalR Documentation](https://docs.microsoft.com/en-us/aspnet/core/signalr/)
