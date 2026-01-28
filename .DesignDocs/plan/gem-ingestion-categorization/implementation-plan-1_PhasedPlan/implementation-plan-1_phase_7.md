---
goal: Implementation Plan for GEM Ingestion, Summarization, and Smart Categorization System
phase_title: LLM Provider Integration & Orchestration
PhaseNumber: 7
version: 1.1
date_created: 2026-01-28
last_updated: 2026-01-28
tags: [llm, ai, openai, semantic-kernel, prompts]
depends_on: [1, 2, 3, 4, 5]
status: Planned
status_color: blue
---

# Introduction

![Status: Planned](https://img.shields.io/badge/Status-Planned-blue)

This phase establishes the foundation for AI-powered features by implementing the LLM provider abstraction layer and concrete implementations for OpenAI and Azure OpenAI. It integrates Microsoft Semantic Kernel for prompt management, implements token counting and cost tracking, and establishes resilience patterns with Polly for reliable LLM API calls.

## 1. Requirements & Constraints

- **REQ-002**: System must generate AI-powered summaries for all ingested content
- **REQ-003**: System must support automatic categorization using AI analysis of content and existing category structure
- **CON-001**: Must use .NET 10.0.2 LTS as primary framework
- **CON-004**: Must follow domain-driven design with clear layer separation
- **CON-005**: Must support both self-hosted (Docker Compose) and future SaaS (K8s-ready) deployment
- **CON-008**: Must abstract LLM provider to support OpenAI, Azure OpenAI, and local models
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
| TASK-031 | Implement LLM Provider abstraction layer (ILLMProvider interface) with methods for completion, embedding generation | | |
| TASK-032 | Implement OpenAI provider using Azure.AI.OpenAI 2.1.0 SDK with configuration for API keys, model selection, temperature, max tokens | | |
| TASK-033 | Implement Azure OpenAI provider as alternative implementation of ILLMProvider using Azure.AI.OpenAI 2.1.0 | | |
| TASK-034 | Create LLM Orchestration Service using Microsoft.SemanticKernel 1.70.0 for prompt management and chaining | | |
| TASK-037 | Implement token counting and cost tracking service to monitor LLM API usage | | |
| TASK-038 | Implement Polly 8.6.5 retry policies and circuit breaker for LLM API calls with exponential backoff | | |
| TASK-056 | Write unit tests using xUnit v3, FluentAssertions 8.8.0, and Moq 4.20.72 for LLM provider abstraction and mock LLM responses for deterministic testing | | |
| TASK-047-P7 | Implement LLM provider factory for runtime provider selection based on configuration | | |

## 3. Alternatives

- **ALT-006**: Local LLM (Ollama, LM Studio) as Primary Provider Instead of OpenAI - Kept as alternative implementation but not primary target due to quality trade-offs

## 4. Dependencies

- **PHASE-DEP-008**: Requires domain model from Phase 3 - Verify GEM and Category entities exist
- **DEP-001**: LLM API Provider (OpenAI or Azure OpenAI) - Critical dependency. Requires API key and sufficient quota
- **DEP-009**: Microsoft.SemanticKernel 1.70.0 - LLM orchestration framework
- **DEP-014**: Polly 8.6.5 - Resilience and fault handling
- **DEP-017**: xUnit v3, FluentAssertions 8.8.0, Moq 4.20.72 - Unit testing frameworks

## 5. Files

- **FILE-021**: `src/InfoDumpManager.Domain/Services/ILLMProvider.cs` - LLM provider abstraction interface
- **FILE-037**: `src/InfoDumpManager.Infrastructure/Services/OpenAILLMProvider.cs` - OpenAI provider implementation
- **FILE-038**: `src/InfoDumpManager.Infrastructure/Services/AzureOpenAILLMProvider.cs` - Azure OpenAI provider implementation
- **FILE-039**: `src/InfoDumpManager.Infrastructure/Services/LLMOrchestrationService.cs` - LLM orchestration with Semantic Kernel
- **FILE-039-P7**: `src/InfoDumpManager.Infrastructure/Services/TokenCountingService.cs` - Token counting and cost tracking
- **FILE-039-P7**: `src/InfoDumpManager.Infrastructure/Services/LLMProviderFactory.cs` - Factory for provider selection
- **FILE-051**: `src/InfoDumpManager.WebAPI/appsettings.json` - Configuration for LLM providers

## 6. Testing

- **TEST-048**: Unit Test - LLM Provider Interface - Mock completion call - Expected: Correct response format
- **TEST-049**: Unit Test - OpenAI Provider - Generate completion with mock client - Expected: Properly formatted request
- **TEST-050**: Unit Test - Token Counting - Count tokens in sample text - Expected: Accurate token count
- **TEST-051**: Unit Test - LLM Orchestration - Chain multiple prompts - Expected: Correct execution sequence
- **TEST-052**: Integration Test - OpenAI Provider - Real API call to OpenAI - Expected: Valid completion response (requires API key)
- **TEST-053**: Integration Test - Polly Retry - Simulate API failure - Expected: Retry with exponential backoff
- **TEST-054**: Integration Test - Circuit Breaker - Multiple failures - Expected: Circuit opens after threshold
- **TEST-055**: Unit Test - Provider Factory - Select provider by config - Expected: Correct provider instance returned

### Test Requirements
- All unit tests must use mocked LLM responses for deterministic results
- Integration tests with real API calls should be optional (require API key)
- Circuit breaker and retry logic must be thoroughly tested
- Token counting accuracy must be validated against known samples

## 7. Risks & Assumptions

- **RISK-014**: LLM API costs may be higher than estimated - Mitigation: Implement token budgets and cost monitoring
- **RISK-015**: LLM API rate limits may be hit during high load - Mitigation: Implement queuing and backoff strategies
- **RISK-016**: API keys must be securely stored - Mitigation: Use environment variables and Azure Key Vault
- **ASSUMPTION-014**: OpenAI API is the primary provider for Phase 7
- **ASSUMPTION-015**: Token counting is approximate and may vary slightly from actual billing

## 8. Success Metrics

- **METRIC-002**: All TEST-XXX tests passing (exit code 0)
- **METRIC-003**: Build successful with no errors (exit code 0)
- **METRIC-026**: LLM completion calls succeed with >99% reliability (after retries)
- **METRIC-027**: Circuit breaker prevents cascading failures during API outages
- **METRIC-028**: Token counting accuracy within 5% of actual API billing
- **METRIC-029**: Provider factory successfully switches between OpenAI and Azure OpenAI

## 9. Related Specifications / Further Reading

- [Azure OpenAI SDK Documentation](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/ai.openai-readme)
- [Microsoft Semantic Kernel Documentation](https://learn.microsoft.com/en-us/semantic-kernel/)
- [OpenAI API Documentation](https://platform.openai.com/docs/api-reference)
- [Polly Resilience Patterns](https://github.com/App-vNext/Polly)
