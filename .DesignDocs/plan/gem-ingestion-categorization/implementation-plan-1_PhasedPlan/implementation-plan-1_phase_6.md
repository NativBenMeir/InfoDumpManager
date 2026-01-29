---
goal: Implementation Plan for GEM Ingestion, Summarization, and Smart Categorization System
phase_title: Web UI - GEM & Category Management
PhaseNumber: 6
version: 1.1
date_created: 2026-01-28
last_updated: 2026-01-28
tags: [ui, web, razor-pages, frontend, wcag]
depends_on: [1, 2, 3, 4, 5]
status: Planned
status_color: blue
---

# Introduction

![Status: Planned](https://img.shields.io/badge/Status-Planned-blue)

This phase delivers the user-facing web interface using ASP.NET Core Razor Pages. It implements GEM submission forms, list views with pagination and filtering, detail views showing snapshots and metadata, and comprehensive category management UI. The interface follows WCAG AA accessibility standards and provides manual category assignment capabilities.

## 1. Requirements & Constraints

- **REQ-005**: System must provide manual category management (create, rename, merge, delete, reassign)
- **CON-001**: Must use .NET 8.0 LTS as primary framework
- **CON-003**: Must use ASP.NET Core for all web applications and APIs
- **CON-004**: Must follow domain-driven design with clear layer separation
- **CON-005**: Must support both self-hosted (Docker Compose) and future SaaS (K8s-ready) deployment
- **NFR-002**: System must be designed for multi-tenant SaaS scalability from day one
- **NFR-003**: All data must be encrypted at rest and in transit
- **NFR-004**: System must provide comprehensive observability (logging, metrics, tracing)
- **NFR-005**: Web UI must meet WCAG AA accessibility standards
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
| TASK-019 | Create basic ASP.NET Core Razor Pages Web UI for GEM submission (URL input form) | | |
| TASK-020 | Create Web UI pages for GEM list view with pagination and basic filtering by category | | |
| TASK-021 | Create Web UI pages for GEM detail view showing title, source link, snapshot preview, and category assignment | | |
| TASK-022 | Create Web UI pages for category management (list, create, edit, delete with confirmation) | | |
| TASK-023 | Implement manual category assignment UI in GEM detail page with dropdown selector | | |
| TASK-044-P6 | Implement responsive design for mobile and tablet devices | | |
| TASK-045-P6 | Add client-side form validation for GEM submission | | |
| TASK-046-P6 | Implement breadcrumb navigation and page titles for all pages | | |
| TASK-TST-P6 | Implement all tests based on per Testing section in this plan. |  |  |

## 3. Alternatives

- **ALT-004**: Blazor WebAssembly SPA Instead of Razor Pages + HTMX - Rejected because server-rendered UI provides simpler architecture, better SEO, and easier accessibility compliance

## 4. Dependencies

- **PHASE-DEP-006**: Requires GEM API from Phase 4 - Verify all CRUD endpoints are functional
- **PHASE-DEP-007**: Requires web scraping from Phase 5 - Verify GEM creation works end-to-end
- **DEP-006**: `src/InfoDumpManager.Web/InfoDumpManager.Web.csproj` - ASP.NET Core Razor Pages web application

## 5. Files

- **FILE-053**: `src/InfoDumpManager.Web/Pages/Index.cshtml` - Home page with GEM submission form
- **FILE-054**: `src/InfoDumpManager.Web/Pages/GEMs/List.cshtml` - GEM list page with filtering
- **FILE-055**: `src/InfoDumpManager.Web/Pages/GEMs/Detail.cshtml` - GEM detail page
- **FILE-056**: `src/InfoDumpManager.Web/Pages/Categories/Manage.cshtml` - Category management page
- **FILE-056-P6**: `src/InfoDumpManager.Web/Pages/Shared/_Layout.cshtml` - Shared layout with navigation
- **FILE-056-P6**: `src/InfoDumpManager.Web/wwwroot/css/site.css` - Custom CSS styles
- **FILE-056-P6**: `src/InfoDumpManager.Web/wwwroot/js/site.js` - Client-side JavaScript
- **FILE-053-P6**: `src/InfoDumpManager.Web/Pages/Index.cshtml.cs` - Page model for home page
- **FILE-054-P6**: `src/InfoDumpManager.Web/Pages/GEMs/List.cshtml.cs` - Page model for GEM list

## 6. Testing

- **TEST-039**: Integration Test - Web UI - Submit GEM via form - Expected: Redirect to GEM detail page
- **TEST-040**: Integration Test - Web UI - List GEMs with pagination - Expected: Correct page displayed
- **TEST-041**: Integration Test - Web UI - Filter GEMs by category - Expected: Only matching GEMs shown
- **TEST-042**: Integration Test - Web UI - View GEM detail - Expected: All GEM fields displayed
- **TEST-043**: Integration Test - Web UI - Assign category to GEM - Expected: Category saved successfully
- **TEST-044**: Integration Test - Web UI - Create category - Expected: Category created and listed
- **TEST-045**: Integration Test - Web UI - Delete category - Expected: Confirmation shown, category deleted
- **TEST-046**: Accessibility Test - Web UI - Run axe DevTools - Expected: No WCAG AA violations
- **TEST-047**: UI Test - Web UI - Mobile responsive - Expected: UI usable on mobile viewport

### Test Requirements
- All page models must have unit tests
- Integration tests must verify full user workflows
- Accessibility must be validated with automated tools (axe DevTools)
- Responsive design must be tested on multiple viewport sizes

## 7. Risks & Assumptions

- **RISK-012**: Accessibility compliance requires ongoing maintenance - Mitigation: Integrate axe DevTools in CI/CD pipeline
- **RISK-013**: Large GEM lists may cause performance issues - Mitigation: Implement server-side pagination with efficient queries
- **ASSUMPTION-012**: Users primarily access UI via desktop browsers
- **ASSUMPTION-013**: Form submissions use POST-Redirect-GET pattern to prevent duplicate submissions

## 8. Success Metrics

- **METRIC-002**: All TEST-XXX tests passing (exit code 0)
- **METRIC-003**: Build successful with no errors (exit code 0)
- **METRIC-022**: Zero WCAG AA violations detected by axe DevTools
- **METRIC-023**: All forms have proper validation and error messages
- **METRIC-024**: Page load times < 2 seconds for typical GEM list views
- **METRIC-025**: Mobile viewport renders correctly on common devices (iPhone, iPad, Android)

## 9. Related Specifications / Further Reading

- [ASP.NET Core Razor Pages Documentation](https://docs.microsoft.com/en-us/aspnet/core/razor-pages/)
- [WCAG 2.1 AA Guidelines](https://www.w3.org/WAI/WCAG21/quickref/?versions=2.1&levels=aa)
- [axe DevTools](https://www.deque.com/axe/devtools/)
- [Responsive Web Design Best Practices](https://web.dev/responsive-web-design-basics/)
