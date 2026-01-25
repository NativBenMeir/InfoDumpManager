---
goal: Complete Phase 1 Test Coverage to Achieve 80%+ Target for TASK-026 and TASK-027
version: 1.0
date_created: 2026-01-25
last_updated: 2026-01-25
owner: Development Team
status: 'Planned'
tags: [testing, phase1, unit-tests, integration-tests, task-026, task-027]
---

# Introduction

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

This implementation plan provides a systematic, phase-by-phase approach to complete all missing tests for Phase 1 of the GEM Ingestion, Summarization, and Smart Categorization system. The plan addresses critical gaps identified in Phase1-Test-Coverage-Analysis.md and ensures completion of TASK-026 (unit tests) and TASK-027 (integration tests) with 80%+ code coverage target.

Current coverage is approximately 30% across domain, application, and infrastructure layers. This plan will bring coverage to 80%+ by implementing 35+ missing test classes covering validators, command handlers, query handlers, repositories, API endpoints, and domain entities.

## 1. Requirements & Constraints

### Testing Requirements
- **REQ-001**: Achieve 80%+ code coverage for domain and application logic (TASK-026)
- **REQ-002**: Complete integration tests for GEM and Category API endpoints using Testcontainers (TASK-027)
- **REQ-003**: All unit tests must use xUnit + FluentAssertions + Moq frameworks
- **REQ-004**: All integration tests must use Testcontainers for PostgreSQL 16
- **REQ-005**: Follow AAA pattern (Arrange-Act-Assert) for all unit tests
- **REQ-006**: Use naming convention: `[Method]_[Scenario]_[ExpectedResult]`
- **REQ-007**: All validator tests must verify both valid and invalid inputs
- **REQ-008**: All handler tests must mock all external dependencies (repositories, services)
- **REQ-009**: All integration tests must use WebApplicationFactory for API testing
- **REQ-010**: All repository tests must verify CRUD operations with actual PostgreSQL database

### Technical Constraints
- **CON-001**: Tests must execute in parallel without side effects
- **CON-002**: Integration tests require Docker Desktop running for Testcontainers
- **CON-003**: All tests must be deterministic and repeatable
- **CON-004**: Mock data must not contain real API keys or secrets
- **CON-005**: Test database must be isolated per test class using Testcontainers
- **CON-006**: All async operations must use CancellationToken.None in tests
- **CON-007**: Tests must not depend on external services (LLM APIs, external web pages)

### Guidelines
- **GUD-001**: Group related tests in the same test class
- **GUD-002**: Use descriptive test method names that explain intent
- **GUD-003**: Each test should verify one specific behavior
- **GUD-004**: Use FluentAssertions for readable assertions (e.g., `result.Should().NotBeNull()`)
- **GUD-005**: Mock only external dependencies, not domain logic
- **GUD-006**: Integration tests should test full request-response cycle
- **GUD-007**: Repository tests should verify database state changes
- **GUD-008**: Use inline test data for simple scenarios, Theory + InlineData for multiple cases

### Test Patterns
- **PAT-001**: Use Moq for mocking interfaces in unit tests
- **PAT-002**: Use Testcontainers PostgreSqlTestcontainer for database integration tests
- **PAT-003**: Use WebApplicationFactory with CustomWebApplicationFactory for API tests
- **PAT-004**: Use IAsyncLifetime for test fixture setup/teardown
- **PAT-005**: Validate FluentValidation rules with validator.Validate(command)
- **PAT-006**: Test command handlers by mocking repository and verifying interactions
- **PAT-007**: Test repositories by executing operations and querying database state

## 2. Implementation Steps

### Implementation Phase 1: Validator Unit Tests (Day 1)

**GOAL-001**: Complete unit tests for all 6 missing FluentValidation validators to ensure input validation coverage

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Create `tests/InfoDumpManager.Tests.Unit/Application/Validators/CreateCategoryCommandValidatorTests.cs` with tests: Validate_ValidCommand_IsSuccessful, Validate_EmptyName_ReturnsValidationFailure, Validate_WhitespaceName_ReturnsValidationFailure | | |
| TASK-002 | Create `tests/InfoDumpManager.Tests.Unit/Application/Validators/UpdateCategoryCommandValidatorTests.cs` with tests: Validate_ValidCommand_IsSuccessful, Validate_EmptyName_ReturnsValidationFailure, Validate_InvalidCategoryId_ReturnsValidationFailure | | |
| TASK-003 | Create `tests/InfoDumpManager.Tests.Unit/Application/Validators/DeleteCategoryCommandValidatorTests.cs` with tests: Validate_ValidCommand_IsSuccessful, Validate_InvalidCategoryId_ReturnsValidationFailure | | |
| TASK-004 | Create `tests/InfoDumpManager.Tests.Unit/Application/Validators/AssignGemToCategoryCommandValidatorTests.cs` with tests: Validate_ValidCommand_IsSuccessful, Validate_InvalidGemId_ReturnsValidationFailure, Validate_InvalidCategoryId_ReturnsValidationFailure | | |
| TASK-005 | Create `tests/InfoDumpManager.Tests.Unit/Application/Validators/RemoveGemFromCategoryCommandValidatorTests.cs` with tests: Validate_ValidCommand_IsSuccessful, Validate_InvalidGemId_ReturnsValidationFailure, Validate_InvalidCategoryId_ReturnsValidationFailure | | |
| TASK-006 | Create `tests/InfoDumpManager.Tests.Unit/Application/Validators/UpdateGemCommandValidatorTests.cs` with tests: Validate_ValidCommand_IsSuccessful, Validate_EmptyTitle_ReturnsValidationFailure, Validate_InvalidGemId_ReturnsValidationFailure (if UpdateGemCommand exists) | | |
| TASK-007 | Run all validator tests: `dotnet test tests/InfoDumpManager.Tests.Unit/Application/Validators --verbosity normal` and verify all pass | | |

### Implementation Phase 2: Category Command Handler Unit Tests (Day 2)

**GOAL-002**: Complete unit tests for all Category command handlers to verify business logic and repository interactions

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-008 | Create `tests/InfoDumpManager.Tests.Unit/Application/Categories/Commands/CreateCategoryCommandHandlerTests.cs` with test: Handle_ValidCommand_CreatesCategory - mock ICategoryRepository.AddAsync, verify category created with correct name/description, verify IUnitOfWork.SaveChangesAsync called | | |
| TASK-009 | Add test to CreateCategoryCommandHandlerTests.cs: Handle_ValidCommand_ReturnsCategoryDto - verify AutoMapper maps entity to DTO correctly | | |
| TASK-010 | Create `tests/InfoDumpManager.Tests.Unit/Application/Categories/Commands/UpdateCategoryCommandHandlerTests.cs` with tests: Handle_ValidCommand_UpdatesCategory - mock repository GetByIdAsync and SaveChangesAsync, verify Rename/UpdateDescription called | | |
| TASK-011 | Add test to UpdateCategoryCommandHandlerTests.cs: Handle_CategoryNotFound_ThrowsException - verify exception when repository returns null | | |
| TASK-012 | Create `tests/InfoDumpManager.Tests.Unit/Application/Categories/Commands/DeleteCategoryCommandHandlerTests.cs` with tests: Handle_ValidCommand_DeletesCategory - mock repository GetByIdAsync and Remove, verify Remove called with correct entity | | |
| TASK-013 | Add test to DeleteCategoryCommandHandlerTests.cs: Handle_CategoryNotFound_ThrowsException - verify exception handling | | |
| TASK-014 | Create `tests/InfoDumpManager.Tests.Unit/Application/Categories/Commands/AssignGemToCategoryCommandHandlerTests.cs` with tests: Handle_ValidCommand_AssignsGemToCategory - mock IGEMRepository and ICategoryRepository, verify AssignGem called on category, verify gem.AssignCategory called | | |
| TASK-015 | Add test to AssignGemToCategoryCommandHandlerTests.cs: Handle_GemOrCategoryNotFound_ThrowsException | | |
| TASK-016 | Create `tests/InfoDumpManager.Tests.Unit/Application/Categories/Commands/RemoveGemFromCategoryCommandHandlerTests.cs` with tests: Handle_ValidCommand_RemovesGemFromCategory - verify RemoveGem called on category and category removed from gem | | |
| TASK-017 | Run all category handler tests: `dotnet test tests/InfoDumpManager.Tests.Unit/Application/Categories --verbosity normal` and verify all pass | | |

### Implementation Phase 3: Query Handler Unit Tests (Day 3)

**GOAL-003**: Complete unit tests for all query handlers (if implemented) to verify read operations and filtering logic

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-018 | Verify existence of query handlers in `src/InfoDumpManager.Application/GEMs/Queries/` and `src/InfoDumpManager.Application/Categories/Queries/` directories - list all query handler files | | |
| TASK-019 | If GetGemByIdQueryHandler exists: Create `tests/InfoDumpManager.Tests.Unit/Application/GEMs/Queries/GetGemByIdQueryHandlerTests.cs` with tests: Handle_ExistingId_ReturnsGemDto, Handle_NonExistingId_ReturnsNull - mock IGEMRepository.GetByIdAsync | | |
| TASK-020 | If GetGemsQueryHandler exists: Create `tests/InfoDumpManager.Tests.Unit/Application/GEMs/Queries/GetGemsQueryHandlerTests.cs` with tests: Handle_WithPagination_ReturnsPagedResults, Handle_WithCategoryFilter_ReturnsFilteredResults - mock repository with test data | | |
| TASK-021 | If GetCategoriesQueryHandler exists: Create `tests/InfoDumpManager.Tests.Unit/Application/Categories/Queries/GetCategoriesQueryHandlerTests.cs` with tests: Handle_ReturnsAllCategories - mock ICategoryRepository.GetAllAsync | | |
| TASK-022 | If GetCategoryByIdQueryHandler exists: Create `tests/InfoDumpManager.Tests.Unit/Application/Categories/Queries/GetCategoryByIdQueryHandlerTests.cs` with tests: Handle_ExistingId_ReturnsCategoryDto, Handle_NonExistingId_ReturnsNull | | |
| TASK-023 | Run all query handler tests if any were created: `dotnet test tests/InfoDumpManager.Tests.Unit/Application --filter "FullyQualifiedName~Queries" --verbosity normal` | | |

### Implementation Phase 4: Domain Entity Tests (Day 3)

**GOAL-004**: Complete unit tests for remaining domain entities (ActivityLog, User if exists) to achieve comprehensive domain coverage

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-024 | Verify ActivityLog entity exists in `src/InfoDumpManager.Domain/Entities/ActivityLog.cs` - examine constructor, properties, and methods | | |
| TASK-025 | Create `tests/InfoDumpManager.Tests.Unit/Domain/ActivityLogTests.cs` with tests: Create_ValidInput_SetsProperties - verify activity type, GEM ID, user ID, timestamp, message are set correctly | | |
| TASK-026 | Add test to ActivityLogTests.cs: Create_InvalidEventType_ThrowsArgumentException (if validation exists) | | |
| TASK-027 | Verify if User entity exists in `src/InfoDumpManager.Domain/Entities/` - if exists, create `tests/InfoDumpManager.Tests.Unit/Domain/UserTests.cs` with basic entity validation tests | | |
| TASK-028 | Verify GEMSnapshot and GEMSummary value objects in `src/InfoDumpManager.Domain/ValueObjects/` - determine if dedicated tests are needed beyond inline GemTests.cs coverage | | |
| TASK-029 | If GEMSnapshot needs dedicated tests: Add GEMSnapshotTests class to `tests/InfoDumpManager.Tests.Unit/Domain/GemTests.cs` file with tests: Create_ValidInput_SetsContentAndType, Create_InvalidContent_ThrowsException | | |
| TASK-030 | If GEMSummary needs dedicated tests: Add GEMSummaryTests class to `tests/InfoDumpManager.Tests.Unit/Domain/GemTests.cs` file with tests: Create_ValidInput_SetsSummaryText, Create_EmptyText_ThrowsException | | |
| TASK-031 | Run all domain tests: `dotnet test tests/InfoDumpManager.Tests.Unit/Domain --verbosity normal` and verify all pass | | |

### Implementation Phase 5: Repository Integration Tests (Day 4)

**GOAL-005**: Create comprehensive integration tests for all repositories using Testcontainers PostgreSQL to verify database operations

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-032 | Create `tests/InfoDumpManager.Tests.Integration/Infrastructure/GEMRepositoryTests.cs` implementing IAsyncLifetime with PostgreSqlTestcontainer setup in InitializeAsync, WebApplicationFactory with test database connection string | | |
| TASK-033 | Add test to GEMRepositoryTests.cs: AddAsync_ValidGem_PersistsToDatabase - create GEM, call AddAsync, commit UnitOfWork, query database directly to verify row exists with correct Title, SourceUrl | | |
| TASK-034 | Add test to GEMRepositoryTests.cs: GetByIdAsync_ExistingGem_ReturnsGem - insert GEM via repository, retrieve by ID, assert properties match | | |
| TASK-035 | Add test to GEMRepositoryTests.cs: GetByIdAsync_NonExistingGem_ReturnsNull - query with random Guid, assert null result | | |
| TASK-036 | Add test to GEMRepositoryTests.cs: UpdateAsync_ExistingGem_SavesChanges - retrieve GEM, modify Title, call UpdateAsync, re-query, verify Title changed in database | | |
| TASK-037 | Add test to GEMRepositoryTests.cs: GetAllAsync_WithPagination_ReturnsPagedResults - insert 25 GEMs, query with skip=10 take=10, verify correct subset returned | | |
| TASK-038 | Add test to GEMRepositoryTests.cs: GetByCategoryIdAsync_WithCategoryFilter_ReturnsFilteredGems - create category, assign 3 GEMs to it, query by category ID, verify only those 3 returned | | |
| TASK-039 | Create `tests/InfoDumpManager.Tests.Integration/Infrastructure/CategoryRepositoryTests.cs` with IAsyncLifetime setup using Testcontainers | | |
| TASK-040 | Add test to CategoryRepositoryTests.cs: AddAsync_ValidCategory_PersistsToDatabase - create category, save, query database, verify Name and Description | | |
| TASK-041 | Add test to CategoryRepositoryTests.cs: GetByIdAsync_ExistingCategory_ReturnsCategory - insert and retrieve category | | |
| TASK-042 | Add test to CategoryRepositoryTests.cs: GetAllAsync_ReturnsAllCategories - insert 5 categories, query all, verify count = 5 | | |
| TASK-043 | Add test to CategoryRepositoryTests.cs: DeleteAsync_ExistingCategory_RemovesFromDatabase - insert category, delete, verify no longer in database | | |
| TASK-044 | Add test to CategoryRepositoryTests.cs: UpdateAsync_ExistingCategory_SavesChanges - insert, modify Name, save, re-query, verify change persisted | | |
| TASK-045 | Create `tests/InfoDumpManager.Tests.Integration/Infrastructure/ActivityLogRepositoryTests.cs` with IAsyncLifetime and Testcontainers setup | | |
| TASK-046 | Add test to ActivityLogRepositoryTests.cs: AddAsync_ValidActivityLog_PersistsToDatabase - create activity log entry for GEM creation, save, query, verify EventType and GemId | | |
| TASK-047 | Add test to ActivityLogRepositoryTests.cs: GetByGemIdAsync_ReturnsRelatedLogs - insert 3 logs for GemId, 2 for another, query first GemId, verify 3 returned | | |
| TASK-048 | Add test to ActivityLogRepositoryTests.cs: GetByDateRangeAsync_ReturnsLogsInRange - insert logs with different timestamps, query date range, verify filtering works | | |
| TASK-049 | Run all repository integration tests: `dotnet test tests/InfoDumpManager.Tests.Integration/Infrastructure --verbosity normal` and verify all pass with Docker running | | |

### Implementation Phase 6: API Integration Tests - Categories (Day 5)

**GOAL-006**: Expand CategoriesController integration tests to cover all CRUD endpoints with full request-response validation

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-050 | Update `tests/InfoDumpManager.Tests.Integration/CategoriesControllerTests.cs` - add test: GetCategoryById_ExistingId_ReturnsCategory - POST category, capture ID, GET by ID, verify response matches | | |
| TASK-051 | Add test to CategoriesControllerTests.cs: GetCategoryById_NonExistingId_ReturnsNotFound - GET with random Guid, assert 404 status code | | |
| TASK-052 | Add test to CategoriesControllerTests.cs: PutCategory_ValidUpdate_UpdatesCategory - POST category, PUT with updated Name, GET to verify change, assert Name updated | | |
| TASK-053 | Add test to CategoriesControllerTests.cs: PutCategory_NonExistingId_ReturnsNotFound - PUT with random Guid, assert 404 | | |
| TASK-054 | Add test to CategoriesControllerTests.cs: PutCategory_InvalidData_ReturnsBadRequest - PUT with empty Name, assert 400 and validation errors in response | | |
| TASK-055 | Add test to CategoriesControllerTests.cs: DeleteCategory_ExistingId_DeletesCategory - POST category, DELETE by ID, GET to verify 404, assert successful deletion | | |
| TASK-056 | Add test to CategoriesControllerTests.cs: DeleteCategory_NonExistingId_ReturnsNotFound - DELETE with random Guid, assert 404 | | |
| TASK-057 | Add test to CategoriesControllerTests.cs: PostCategory_InvalidData_ReturnsBadRequest - POST with empty Name, assert 400 and validation errors | | |
| TASK-058 | Run categories controller tests: `dotnet test tests/InfoDumpManager.Tests.Integration --filter "FullyQualifiedName~CategoriesControllerTests" --verbosity normal` and verify all pass | | |

### Implementation Phase 7: API Integration Tests - GEMs (Day 6)

**GOAL-007**: Create comprehensive integration tests for GemsController covering all endpoints with snapshot storage validation

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-059 | Create `tests/InfoDumpManager.Tests.Integration/GemsControllerTests.cs` with IAsyncLifetime setup using PostgreSqlTestcontainer and CustomWebApplicationFactory | | |
| TASK-060 | Add test to GemsControllerTests.cs: PostGem_ValidInput_CreatesGem - POST to /api/v1/gems with Url and Title, assert 201 status, verify response DTO has ID, Title, SourceUrl populated | | |
| TASK-061 | Add test to GemsControllerTests.cs: PostGem_InvalidUrl_ReturnsBadRequest - POST with malformed URL, assert 400 and validation errors for Url property | | |
| TASK-062 | Add test to GemsControllerTests.cs: PostGem_EmptyTitle_ReturnsBadRequest - POST with empty Title, assert 400 and validation errors | | |
| TASK-063 | Add test to GemsControllerTests.cs: PostGem_CreatesSnapshot_StoresInDatabase - POST gem, retrieve by ID, verify SnapshotContent is not null (or SnapshotUrl exists depending on implementation) | | |
| TASK-064 | Add test to GemsControllerTests.cs: GetGems_ReturnsPagedList - POST 3 gems, GET /api/v1/gems, assert response is PaginatedResponse with 3 items | | |
| TASK-065 | Add test to GemsControllerTests.cs: GetGems_WithPagination_ReturnsCorrectPage - POST 15 gems, GET with ?page=2&pageSize=5, verify items 6-10 returned | | |
| TASK-066 | Add test to GemsControllerTests.cs: GetGems_WithCategoryFilter_ReturnsFilteredGems - POST 3 gems, assign 2 to category, GET with ?categoryId={id}, verify only 2 returned | | |
| TASK-067 | Add test to GemsControllerTests.cs: GetGemById_ExistingId_ReturnsGem - POST gem, GET by ID, verify all properties match including Source, Snapshot, CategoryIds | | |
| TASK-068 | Add test to GemsControllerTests.cs: GetGemById_NonExistingId_ReturnsNotFound - GET with random Guid, assert 404 | | |
| TASK-069 | If PUT /api/v1/gems/{id} endpoint exists: Add test UpdateGem_ValidInput_UpdatesGem - POST gem, PUT with updated Title, GET to verify change | | |
| TASK-070 | If DELETE endpoint exists: Add test DeleteGem_ExistingId_DeletesGem - POST gem, DELETE by ID, GET to verify 404 | | |
| TASK-071 | Run gems controller tests: `dotnet test tests/InfoDumpManager.Tests.Integration --filter "FullyQualifiedName~GemsControllerTests" --verbosity normal` and verify all pass | | |

### Implementation Phase 8: Coverage Validation & Reporting (Day 6)

**GOAL-008**: Validate 80%+ code coverage target is met and generate comprehensive test coverage reports

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-072 | Run full test suite with coverage collection: `dotnet test /p:CollectCoverage=true /p:CoverageFormat=opencover /p:CoverageDirectory=./coverage` | | |
| TASK-073 | Install ReportGenerator if not present: `dotnet tool install -g dotnet-reportgenerator-globaltool` | | |
| TASK-074 | Generate HTML coverage report: `reportgenerator -reports:"./coverage/coverage.opencover.xml" -targetdir:"./coverage/report" -reporttypes:Html` | | |
| TASK-075 | Open coverage report in browser, navigate to Domain project, verify 80%+ line coverage for all entities (GEM, Category, ActivityLog) | | |
| TASK-076 | Verify Application project has 80%+ coverage for all validators and command handlers | | |
| TASK-077 | Verify Infrastructure repositories have 80%+ coverage (integration tests count toward this) | | |
| TASK-078 | If coverage is below 80% in any critical area, identify gaps: run `dotnet test --verbosity normal --collect:"XPlat Code Coverage"` and analyze uncovered lines | | |
| TASK-079 | Create additional tests for any uncovered critical paths identified in TASK-078 | | |
| TASK-080 | Run final test suite: `dotnet test --verbosity normal` and verify all tests pass (0 failures) | | |
| TASK-081 | Verify Docker is running and integration tests execute successfully: `dotnet test tests/InfoDumpManager.Tests.Integration --verbosity normal` | | |
| TASK-082 | Update Phase1-Test-Coverage-Analysis.md with final coverage statistics and mark TASK-026 and TASK-027 as complete | | |
| TASK-083 | Commit all test files to version control with commit message: "Complete Phase 1 test coverage - TASK-026, TASK-027 (80%+ coverage achieved)" | | |

## 3. Alternatives

**ALT-001**: Use NSubstitute instead of Moq for mocking - Rejected because existing tests already use Moq, and consistency is more valuable than marginal syntax differences. Moq is widely adopted and well-documented.

**ALT-002**: Use MSTest or NUnit instead of xUnit - Rejected because project is already configured with xUnit, and xUnit provides better parallel execution and integration with modern .NET tooling.

**ALT-003**: Use in-memory EF Core DbContext for repository tests instead of Testcontainers - Rejected because in-memory provider does not support all PostgreSQL features (e.g., unique constraints, triggers, pgvector extension). Real database testing provides higher confidence.

**ALT-004**: Write integration tests using Postman/Newman collection - Rejected because code-based tests with WebApplicationFactory provide type safety, better IDE support, and integration with CI/CD pipelines.

**ALT-005**: Skip query handler tests if handlers don't exist yet - Accepted if queries are not implemented in Phase 1. TASK-018 verifies existence before creating tests.

**ALT-006**: Combine all validator tests into single ValidatorTests.cs file - Rejected because separate files per validator improve maintainability and parallel test execution.

**ALT-007**: Use FluentAssertions for all assertions instead of xUnit Assert - Considered but not required. Existing tests use xUnit Assert, but FluentAssertions can be used for new tests for better readability (e.g., `result.Should().NotBeNull()`).

**ALT-008**: Write tests for Web UI (Razor Pages) with Playwright - Deferred to Phase 4. Phase 1 focuses on API and domain/application logic coverage.

## 4. Dependencies

**DEP-001**: xUnit test framework (already installed in test projects)
**DEP-002**: Moq mocking library (already installed)
**DEP-003**: FluentAssertions for readable assertions (verify in test project .csproj files)
**DEP-004**: Testcontainers.PostgreSql NuGet package (already installed in integration test project)
**DEP-005**: Microsoft.AspNetCore.Mvc.Testing for WebApplicationFactory (already installed)
**DEP-006**: Docker Desktop running for integration tests (manual prerequisite)
**DEP-007**: .NET 8 SDK (already installed)
**DEP-008**: dotnet-reportgenerator-globaltool for coverage reports (TASK-073 installs)
**DEP-009**: AutoMapper (already used in CreateGemCommandHandlerTests.cs)
**DEP-010**: Access to src/InfoDumpManager.* projects for referencing validators, handlers, repositories

## 5. Files

**FILE-001**: `tests/InfoDumpManager.Tests.Unit/Application/Validators/CreateCategoryCommandValidatorTests.cs` (new)
**FILE-002**: `tests/InfoDumpManager.Tests.Unit/Application/Validators/UpdateCategoryCommandValidatorTests.cs` (new)
**FILE-003**: `tests/InfoDumpManager.Tests.Unit/Application/Validators/DeleteCategoryCommandValidatorTests.cs` (new)
**FILE-004**: `tests/InfoDumpManager.Tests.Unit/Application/Validators/AssignGemToCategoryCommandValidatorTests.cs` (new)
**FILE-005**: `tests/InfoDumpManager.Tests.Unit/Application/Validators/RemoveGemFromCategoryCommandValidatorTests.cs` (new)
**FILE-006**: `tests/InfoDumpManager.Tests.Unit/Application/Validators/UpdateGemCommandValidatorTests.cs` (new)
**FILE-007**: `tests/InfoDumpManager.Tests.Unit/Application/Categories/Commands/CreateCategoryCommandHandlerTests.cs` (new)
**FILE-008**: `tests/InfoDumpManager.Tests.Unit/Application/Categories/Commands/UpdateCategoryCommandHandlerTests.cs` (new)
**FILE-009**: `tests/InfoDumpManager.Tests.Unit/Application/Categories/Commands/DeleteCategoryCommandHandlerTests.cs` (new)
**FILE-010**: `tests/InfoDumpManager.Tests.Unit/Application/Categories/Commands/AssignGemToCategoryCommandHandlerTests.cs` (new)
**FILE-011**: `tests/InfoDumpManager.Tests.Unit/Application/Categories/Commands/RemoveGemFromCategoryCommandHandlerTests.cs` (new)
**FILE-012**: `tests/InfoDumpManager.Tests.Unit/Application/GEMs/Queries/GetGemByIdQueryHandlerTests.cs` (new, conditional)
**FILE-013**: `tests/InfoDumpManager.Tests.Unit/Application/GEMs/Queries/GetGemsQueryHandlerTests.cs` (new, conditional)
**FILE-014**: `tests/InfoDumpManager.Tests.Unit/Application/Categories/Queries/GetCategoriesQueryHandlerTests.cs` (new, conditional)
**FILE-015**: `tests/InfoDumpManager.Tests.Unit/Application/Categories/Queries/GetCategoryByIdQueryHandlerTests.cs` (new, conditional)
**FILE-016**: `tests/InfoDumpManager.Tests.Unit/Domain/ActivityLogTests.cs` (new)
**FILE-017**: `tests/InfoDumpManager.Tests.Unit/Domain/UserTests.cs` (new, conditional)
**FILE-018**: `tests/InfoDumpManager.Tests.Unit/Domain/GemTests.cs` (update with GEMSnapshot/GEMSummary tests if needed)
**FILE-019**: `tests/InfoDumpManager.Tests.Integration/Infrastructure/GEMRepositoryTests.cs` (new)
**FILE-020**: `tests/InfoDumpManager.Tests.Integration/Infrastructure/CategoryRepositoryTests.cs` (new)
**FILE-021**: `tests/InfoDumpManager.Tests.Integration/Infrastructure/ActivityLogRepositoryTests.cs` (new)
**FILE-022**: `tests/InfoDumpManager.Tests.Integration/CategoriesControllerTests.cs` (update with 7 new tests)
**FILE-023**: `tests/InfoDumpManager.Tests.Integration/GemsControllerTests.cs` (new)
**FILE-024**: `docs/ways-of-work/plan/gem-ingestion-categorization/Phase1-Test-Coverage-Analysis.md` (update with final stats)

## 6. Testing

**TEST-001**: All validator tests must pass with both valid and invalid inputs
**TEST-002**: All handler tests must verify repository method calls using Moq.Verify
**TEST-003**: All repository integration tests must verify database state changes by re-querying
**TEST-004**: All API integration tests must verify HTTP status codes and response DTOs
**TEST-005**: Coverage report must show 80%+ line coverage for Domain project
**TEST-006**: Coverage report must show 80%+ line coverage for Application validators and handlers
**TEST-007**: All tests must pass in parallel execution (xUnit default)
**TEST-008**: Integration tests must pass with clean PostgreSQL container each run
**TEST-009**: No tests should depend on execution order
**TEST-010**: All async tests must complete within 30 seconds (use CancellationToken with timeout if needed)

## 7. Risks & Assumptions

**RISK-001**: Docker Desktop may not be running when developers execute integration tests - Mitigation: Add clear error message in test setup and document requirement in README
**RISK-002**: Testcontainers may fail on some CI/CD environments - Mitigation: Ensure CI has Docker support, use skip logic if Docker unavailable
**RISK-003**: Query handlers may not exist yet in Phase 1 - Mitigation: TASK-018 verifies existence before creating tests
**RISK-004**: Test execution time may be slow due to Testcontainers startup - Mitigation: Use IAsyncLifetime to share container across tests in same class
**RISK-005**: Achieving 80%+ coverage may reveal missing implementation code - Mitigation: Flag missing implementations and address in separate tasks
**RISK-006**: Mock configurations may not reflect actual service behavior - Mitigation: Supplement with integration tests that use real implementations

**ASSUMPTION-001**: All validators exist in `src/InfoDumpManager.Application/Validators/` directory
**ASSUMPTION-002**: All command handlers exist in `src/InfoDumpManager.Application/{Feature}/Commands/Handlers/` directories
**ASSUMPTION-003**: CustomWebApplicationFactory is already configured correctly in `tests/InfoDumpManager.Tests.Integration/Infrastructure/`
**ASSUMPTION-004**: PostgreSQL migrations are applied automatically by CustomWebApplicationFactory during test setup
**ASSUMPTION-005**: Existing test infrastructure (GlobalUsings.cs, TestcontainersSetup.cs) is functional
**ASSUMPTION-006**: All repository interfaces (IGEMRepository, ICategoryRepository, IActivityLogRepository) are implemented in Infrastructure layer
**ASSUMPTION-007**: FluentValidation is configured to validate all commands automatically via MediatR pipeline (or validators are called explicitly in tests)
**ASSUMPTION-008**: AutoMapper profiles exist for all DTO mappings (GemDto, CategoryDto, etc.)

## 8. Related Specifications / Further Reading

- [Phase 1 Test Coverage Analysis](./gem-ingestion-categorization/Phase1-Test-Coverage-Analysis.md) - Detailed gap analysis that motivated this plan
- [GEM Ingestion Implementation Plan](./gem-ingestion-categorization/implementation-plan-1.md) - TASK-026 and TASK-027 original specifications
- [AGENTS.md - Testing Instructions](../../AGENTS.md#testing-instructions) - Testing conventions and patterns
- [xUnit Documentation](https://xunit.net/) - Official xUnit framework documentation
- [Moq Quickstart](https://github.com/moq/moq4/wiki/Quickstart) - Moq mocking library guide
- [FluentAssertions Documentation](https://fluentassertions.com/introduction) - Assertion library syntax
- [Testcontainers for .NET](https://dotnet.testcontainers.org/) - Container-based integration testing
- [Microsoft.AspNetCore.Mvc.Testing](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests) - WebApplicationFactory guide
- [Code Coverage with Coverlet](https://github.com/coverlet-coverage/coverlet) - .NET code coverage tool