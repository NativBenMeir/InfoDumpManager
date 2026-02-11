# Phase 5: Test Updates & Final Verification

status: 'Completed'

![Status: Completed](https://img.shields.io/badge/status-Completed-brightgreen)

**Goal:** Update all broken tests after Phases 1–4, add tests for new components, and perform a full verification.

**Prerequisites:** Phase 4 complete and `dotnet build` succeeding.

**Validation:** `dotnet test` passes all tests. `dotnet build -c Release` succeeds.

---

## Implementation

- TASK-AUT: Implement all unit tests based on Testing section in this plan
- TASK-AIT: Implement all integration tests based on Testing section in this plan

## 5.1 — Fix Tests That Reference Moved Types

### User Entity Moved to Infrastructure

Any test file importing `InfoDumpManager.Domain.Entities.User` must change to `InfoDumpManager.Infrastructure.Data.Entities.User`.

**Files likely affected** (search for `using InfoDumpManager.Domain.Entities` in test projects and check for `User` references):
- `tests/InfoDumpManager.Tests.Integration/ApiIntegrationTests.cs`
- `tests/InfoDumpManager.Tests.Integration/Fixtures/` (any fixture that creates Users)
- `tests/InfoDumpManager.Tests.Unit/` (any unit test that tests User behavior)

For each file:
```csharp
// BEFORE
using InfoDumpManager.Domain.Entities;
// If User is the only reason for this using, change to:
using InfoDumpManager.Infrastructure.Data.Entities;
// If both domain entities AND User are needed, add BOTH usings.
```

### InMemoryJobQueue Moved to Infrastructure

Test files referencing `InfoDumpManager.Application.Infrastructure.JobQueue.InMemoryJobQueue`:
- `tests/InfoDumpManager.Tests.Unit/AIAgents/JobQueueTests.cs`
- `tests/InfoDumpManager.Tests.Integration/AIAgentsProcessingIntegrationTests.cs`
- `tests/InfoDumpManager.Tests.Integration/GemIngestionIntegrationTests.cs`
- `tests/InfoDumpManager.Tests.Integration/AIAgents/JobQueuePersistenceTests.cs`

Change:
```csharp
// BEFORE
using InfoDumpManager.Application.Infrastructure.JobQueue;
// AFTER (for InMemoryJobQueue class only)
using InfoDumpManager.Infrastructure.Services;
// Keep the Application using for IJobQueue<T> and ProcessingJob:
using InfoDumpManager.Application.Infrastructure.JobQueue;
```

### ContentProcessingBackgroundService Moved to Infrastructure

Test files referencing `InfoDumpManager.Application.Services.ContentProcessingBackgroundService`:
```csharp
// BEFORE
using InfoDumpManager.Application.Services;
// AFTER
using InfoDumpManager.Infrastructure.Services;
```

---

## 5.2 — Fix Tests That Use ContentProcessingOrchestrator

The orchestrator now requires `IJobTracker` in its constructor.

**Files likely affected:**
- `tests/InfoDumpManager.Tests.Integration/AIAgentsProcessingIntegrationTests.cs`
- Any test that constructs `ContentProcessingOrchestrator` directly

Update constructor calls:
```csharp
// BEFORE
var orchestrator = new ContentProcessingOrchestrator(scopeFactory, logger);

// AFTER
var jobTracker = new InMemoryJobTracker();
var orchestrator = new ContentProcessingOrchestrator(scopeFactory, jobTracker, logger);
```

---

## 5.3 — Fix Tests That Use UnitOfWork

The `UnitOfWork` constructor now requires all repository instances.

**Files likely affected:**
- `tests/InfoDumpManager.Tests.Integration/EFCoreIntegrationTests.cs`
- `tests/InfoDumpManager.Tests.Integration/RepositoryIntegrationTests.cs`
- Any test that constructs `UnitOfWork` directly

Update constructor calls:
```csharp
// BEFORE
var unitOfWork = new UnitOfWork(context);

// AFTER
var gemRepo = new GEMRepository(context);
var categoryRepo = new CategoryRepository(context);
var tagRepo = new TagRepository(context);
var categorySuggestionRepo = new CategorySuggestionRepository(context);
var activityLogRepo = new ActivityLogRepository(context);
var unitOfWork = new UnitOfWork(context, gemRepo, categoryRepo, tagRepo, categorySuggestionRepo, activityLogRepo);
```

Or use mocks if the test only needs certain repositories:
```csharp
var unitOfWork = new UnitOfWork(
    context,
    Mock.Of<IGEMRepository>(),
    Mock.Of<ICategoryRepository>(),
    Mock.Of<ITagRepository>(),
    Mock.Of<ICategorySuggestionRepository>(),
    Mock.Of<IActivityLogRepository>());
```

---

## 5.4 — Add Tests for New Components

### Test: ValidationBehavior

**New File: `tests/InfoDumpManager.Tests.Unit/Common/ValidationBehaviorTests.cs`**

```csharp
// Test 1: When validators pass, handler is invoked and result returned.
// Test 2: When a validator fails, ValidationException is thrown with correct errors.
// Test 3: When no validators are registered (empty IEnumerable), handler is invoked.

[Fact]
public async Task Handle_WithNoValidators_CallsNext()
{
    // Arrange
    var validators = Enumerable.Empty<IValidator<TestCommand>>();
    var behavior = new ValidationBehavior<TestCommand, TestResult>(validators);
    var command = new TestCommand();

    // Act
    var result = await behavior.Handle(command, () => Task.FromResult(new TestResult()), CancellationToken.None);

    // Assert
    Assert.NotNull(result);
}

[Fact]
public async Task Handle_WithFailingValidator_ThrowsValidationException()
{
    // Arrange - create a mock validator that returns a failure
    var validator = new Mock<IValidator<TestCommand>>();
    validator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestCommand>>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new ValidationResult(new[] { new ValidationFailure("Title", "Title is required.") }));

    var behavior = new ValidationBehavior<TestCommand, TestResult>(new[] { validator.Object });

    // Act & Assert
    await Assert.ThrowsAsync<ValidationException>(
        () => behavior.Handle(new TestCommand(), () => Task.FromResult(new TestResult()), CancellationToken.None));
}
```

### Test: AggregateRoot Domain Events

**New File: `tests/InfoDumpManager.Tests.Unit/Common/AggregateRootDomainEventsTests.cs`**

```csharp
[Fact]
public void RaiseDomainEvent_AddsEventToCollection()
{
    // Use a concrete subclass that exposes RaiseDomainEvent
    var entity = TestAggregate.Create();
    entity.DoSomethingThatRaisesEvent();

    Assert.Single(entity.DomainEvents);
}

[Fact]
public void ClearDomainEvents_RemovesAllEvents()
{
    var entity = TestAggregate.Create();
    entity.DoSomethingThatRaisesEvent();
    entity.ClearDomainEvents();

    Assert.Empty(entity.DomainEvents);
}

// TestAggregate is a private test helper class that extends AggregateRoot<Guid>
```

### Test: InMemoryJobTracker

**New File: `tests/InfoDumpManager.Tests.Unit/AIAgents/JobTrackerTests.cs`**

```csharp
[Fact]
public async Task UpdateStatus_MakesStatusRetrievable()
{
    var tracker = new InMemoryJobTracker();
    var jobId = Guid.NewGuid();

    tracker.UpdateStatus(jobId, ProcessingStatus.Processing, 50, "Half done");

    var status = await tracker.GetJobStatusAsync(jobId);

    Assert.Equal(ProcessingStatus.Processing, status.Status);
    Assert.Equal(50, status.ProgressPercent);
}

[Fact]
public async Task GetJobStatusAsync_ForUnknownJob_ReturnsPending()
{
    var tracker = new InMemoryJobTracker();
    var status = await tracker.GetJobStatusAsync(Guid.NewGuid());
    Assert.Equal(ProcessingStatus.Pending, status.Status);
}
```

---

## 5.5 — Full Verification

Run the following commands in order:

```bash
# 1. Clean build
dotnet clean
dotnet build

# 2. Run all unit tests
dotnet test tests/InfoDumpManager.Tests.Unit -v n

# 3. Run all integration tests (requires Docker services running)
dotnet test tests/InfoDumpManager.Tests.Integration -v n

# 4. Release build
dotnet build -c Release
```

All must pass with zero errors.

---

## Phase 5 Completion Checklist

- [x] All `using` statements updated for moved types (`User`, `InMemoryJobQueue`, `BackgroundService`).
- [x] Test constructor calls updated for `UnitOfWork` (now requires repository params).
- [x] Test constructor calls updated for `ContentProcessingOrchestrator` (now requires `IJobTracker`).
- [x] New unit tests for `ValidationBehavior`.
- [x] New unit tests for `AggregateRoot` domain events.
- [x] New unit tests for `InMemoryJobTracker`.
- [x] `dotnet build` succeeds (Debug + Release).
- [x] `dotnet test tests/InfoDumpManager.Tests.Unit` — all pass.
- [x] `dotnet test tests/InfoDumpManager.Tests.Integration` — all pass.
