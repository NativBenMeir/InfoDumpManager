---
goal: Implementation Plan for GEM Ingestion, Summarization, and Smart Categorization System
phase_title: Observability, Security & Production Readiness
PhaseNumber: 11
version: 1.1
date_created: 2026-01-28
last_updated: 2026-01-28
tags: [observability, security, production, monitoring, performance]
depends_on: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]
status: Planned
status_color: blue
---

# Introduction

![Status: Planned](https://img.shields.io/badge/Status-Planned-blue)

This final phase hardens the system for production deployment by implementing comprehensive observability (logging, metrics, tracing), conducting security audits, optimizing performance, and establishing operational tooling. It delivers production-ready infrastructure with monitoring dashboards, health checks, automated backups, load testing validation, and complete documentation for deployment and operations.

## 1. Requirements & Constraints

- **CON-001**: Must use .NET 10.0.2 LTS as primary framework
- **CON-004**: Must follow domain-driven design with clear layer separation
- **CON-005**: Must support both self-hosted (Docker Compose) and future SaaS (K8s-ready) deployment
- **CON-007**: All services must be containerized via Docker
- **NFR-001**: Ingestion + summarization must complete in < 15 seconds (p95) for typical web pages
- **NFR-002**: System must be designed for multi-tenant SaaS scalability from day one
- **NFR-003**: All data must be encrypted at rest and in transit
- **NFR-004**: System must provide comprehensive observability (logging, metrics, tracing)
- **NFR-005**: Web UI must meet WCAG AA accessibility standards
- **NFR-006**: System must handle tens of GEMs per user per day
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
| TASK-096 | Set up Seq or ELK stack for centralized log aggregation and configure Serilog 4.3.0 sinks | | |
| TASK-097 | Configure structured logging with correlation IDs for request tracing across services | | |
| TASK-098 | Implement OpenTelemetry 1.15.0 instrumentation for distributed tracing with Jaeger or Application Insights | | |
| TASK-099 | Set up Prometheus metrics exporters using prometheus-net in all API and background services | | |
| TASK-100 | Create Grafana dashboards for KPIs: GEMs created per day, summarization latency, LLM token usage, search queries, categorization accuracy | | |
| TASK-101 | Implement health check endpoints for all services: /health/live (liveness) and /health/ready (readiness) | | |
| TASK-102 | Configure ASP.NET Core health checks for database, Redis, MinIO, and LLM provider connectivity | | |
| TASK-103 | Profile application performance using dotnet-trace and identify top 5 bottlenecks | | |
| TASK-104 | Evaluate query performance for GEM list, search, and category views; introduce Dapper for high-latency paths if needed | | |
| TASK-124 | Conduct accessibility audit using axe DevTools and fix WCAG AA violations (NFR-005) | | |

## 3. Alternatives

- **ALT-005**: Hangfire for Background Jobs Instead of IHostedService - Can be added now if monitoring requirements justify it
- **ALT-003**: RabbitMQ or Azure Service Bus for Job Queue - Consider for future multi-instance deployments

## 4. Dependencies

- **PHASE-DEP-015**: Requires all previous phases complete - Verify system is fully functional end-to-end
- **DEP-015**: prometheus-net - Metrics collection library
- **DEP-018**: k6 or JMeter - Load testing tools
- **DEP-019**: Seq or ELK Stack - Log aggregation platform
- **DEP-020**: Grafana + Prometheus - Metrics and dashboards
- **DEP-021**: nginx or Traefik - Reverse proxy and load balancer

## 5. Files

- **FILE-061**: `docker-compose.prod.yml` - Production Docker Compose configuration
- **FILE-062**: `Dockerfile.webapi` - Dockerfile for Web API service
- **FILE-063**: `Dockerfile.web` - Dockerfile for Web UI service
- **FILE-061-P11**: `infrastructure/grafana/dashboards/gem-system.json` - Grafana dashboard configuration
- **FILE-061-P11**: `infrastructure/prometheus/prometheus.yml` - Prometheus configuration
- **FILE-061-P11**: `infrastructure/nginx/nginx.conf` - nginx reverse proxy configuration
- **FILE-061-P11**: `docs/deployment/production-deployment.md` - Production deployment guide
- **FILE-061-P11**: `docs/deployment/backup-restore.md` - Backup and restore procedures
- **FILE-061-P11**: `docs/operations/runbook.md` - Operational runbook
- **FILE-061-P11**: `docs/operations/troubleshooting.md` - Troubleshooting guide

## 6. Testing

- **TEST-081**: Integration Test - Health Checks - All services healthy - Expected: /health/ready returns 200
- **TEST-082**: Integration Test - Health Checks - Database down - Expected: /health/ready returns 503
- **TEST-083**: Load Test - GEM Creation - 100 concurrent users - Expected: p95 < 15 seconds (NFR-001)
- **TEST-084**: Load Test - Search - 50 concurrent queries - Expected: p95 < 2 seconds
- **TEST-085**: Security Test - SQL Injection - Malicious input - Expected: Properly sanitized, no injection
- **TEST-086**: Security Test - XSS Attack - Script in GEM title - Expected: Escaped in UI
- **TEST-087**: Accessibility Test - WCAG AA Compliance - Automated scan - Expected: Zero violations
- **TEST-088**: Performance Test - Database Queries - Slow query log - Expected: No queries > 1 second
- **TEST-089**: Integration Test - Metrics Export - Prometheus scrape - Expected: All metrics available
- **TEST-090**: Integration Test - Distributed Tracing - End-to-end trace - Expected: Complete trace in Jaeger

### Test Requirements
- Load testing must validate NFR-001 (< 15 seconds p95 latency)
- Security testing must cover OWASP Top 10 vulnerabilities
- Accessibility must meet WCAG AA standards (NFR-005)
- All health checks must be validated in failure scenarios

## 7. Risks & Assumptions

- **RISK-026**: Performance bottlenecks may not be discovered until load testing - Mitigation: Early profiling and optimization
- **RISK-027**: Security vulnerabilities may exist in dependencies - Mitigation: Regular security scanning and updates
- **RISK-028**: Production environment may differ from development - Mitigation: Use identical Docker images in all environments
- **ASSUMPTION-022**: Production environment has sufficient resources (16GB RAM, 4 CPU cores minimum)
- **ASSUMPTION-023**: Automated backup solution (e.g., pg_dump) is configured externally

## 8. Success Metrics

- **METRIC-002**: All TEST-XXX tests passing (exit code 0)
- **METRIC-003**: Build successful with no errors (exit code 0)
- **METRIC-043**: Load testing validates NFR-001: p95 latency < 15 seconds for ingestion + summarization
- **METRIC-044**: Zero critical or high severity security vulnerabilities
- **METRIC-022**: Zero WCAG AA violations (NFR-005)
- **METRIC-045**: Health check endpoints respond within 100ms
- **METRIC-046**: All Grafana dashboards display real-time metrics correctly
- **METRIC-047**: Distributed tracing captures >95% of requests end-to-end
- **METRIC-048**: Database backup completes successfully and restore tested
- **METRIC-049**: Production deployment completes without errors
- **METRIC-050**: All operational documentation complete and validated

## 9. Related Specifications / Further Reading

- [OpenTelemetry .NET Documentation](https://opentelemetry.io/docs/instrumentation/net/)
- [Prometheus Best Practices](https://prometheus.io/docs/practices/)
- [Grafana Dashboard Design](https://grafana.com/docs/grafana/latest/dashboards/)
- [ASP.NET Core Health Checks](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)
- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [Docker Production Best Practices](https://docs.docker.com/develop/dev-best-practices/)
