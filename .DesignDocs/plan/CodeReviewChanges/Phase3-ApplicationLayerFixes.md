# Phase 3: Application Layer Fixes

status: 'Completed'

![Status: Completed](https://img.shields.io/badge/status-Completed-brightgreen)

**Goal:** Add a MediatR validation pipeline behavior, remove the duplicate application-layer validator, refactor the orchestrator, and move the `BackgroundService` out of the Application layer.

**Prerequisites:** Phase 2 complete and building.

**Validation:** `dotnet build` and `dotnet test` from solution root.

---

## 3.1 — Add MediatR Validation Pipeline Behavior

### Problem
There is no `ValidationBehavior` in the MediatR pipeline. FluentValidation only runs if ASP.NET model binding triggers it. Commands sent from the Web UI pages or background services bypass FluentValidation entirely.

### New File: `src/InfoDumpManager.Application/Common/Behaviors/ValidationBehavior.cs`

```csharp
using FluentValidation;
using MediatR;

namespace InfoDumpManager.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that runs FluentValidation validators
/// before the handler executes. Throws ValidationException on failure.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next();
    }
}
```

### Registration

In the MediatR configuration (currently in WebAPI `Program.cs` and Web `Program.cs`), add the behavior. This will move to the shared extension method in Phase 4, but for now add it wherever MediatR is registered:

```csharp
services.AddMediatR(configuration =>
{
    configuration.RegisterServicesFromAssembly(typeof(AssemblyReference).Assembly);
});

// ADD this line:
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
```

### Handle ValidationException in ErrorHandlingMiddleware

**File: `src/InfoDumpManager.WebAPI/Middleware/ErrorHandlingMiddleware.cs`**

Update the `InvokeAsync` method to catch `FluentValidation.ValidationException` specifically and return a 400:

```csharp
public async Task InvokeAsync(HttpContext context)
{
    try
    {
        await _next(context);
    }
    catch (FluentValidation.ValidationException validationException)
    {
        Log.Warning(validationException, "Validation failed for {Path}", context.Request.Path);
        await WriteValidationProblemDetailsAsync(context, validationException);
    }
    catch (Exception exception)
    {
        Log.Error(exception, "Unhandled exception while processing {Path}", context.Request.Path);
        await WriteProblemDetailsAsync(context, exception);
    }
}

private static Task WriteValidationProblemDetailsAsync(HttpContext context, FluentValidation.ValidationException exception)
{
    context.Response.ContentType = "application/problem+json";
    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;

    var errors = exception.Errors
        .GroupBy(e => e.PropertyName)
        .ToDictionary(
            g => g.Key,
            g => g.Select(e => e.ErrorMessage).ToArray());

    var problemDetails = new ValidationProblemDetails(errors)
    {
        Status = (int)HttpStatusCode.BadRequest,
        Title = "One or more validation errors occurred.",
        Instance = context.Request.Path
    };

    var payload = JsonSerializer.Serialize(problemDetails, SerializerOptions);
    return context.Response.WriteAsync(payload);
}
```

Add `using FluentValidation;` and `using Microsoft.AspNetCore.Mvc;` to the file.

---

## 3.2 — Remove Duplicate Application-Layer Command Validator

### Problem
`CreateGEMCommandValidator` in `src/InfoDumpManager.Application/Validators/CreateGEMCommandValidator.cs` is nearly identical to `CreateGemRequestValidator` in `src/InfoDumpManager.WebAPI/Validators/GEMs/CreateGemRequestValidator.cs`. With the `ValidationBehavior` pipeline (3.1), the Application-layer validator will now automatically run for all commands, so we should keep it and remove the near-duplicate API-level validator.

### Decision
**Keep** the application-layer validators (`CreateGEMCommandValidator`, `CreateCategoryCommandValidator`, `AssignCategoryCommandValidator`) — they validate MediatR commands and will be executed automatically by `ValidationBehavior`.

**Delete** the WebAPI-layer validators that duplicate command validation:
- `src/InfoDumpManager.WebAPI/Validators/GEMs/CreateGemRequestValidator.cs` — DELETE
- `src/InfoDumpManager.WebAPI/Validators/GEMs/AssignCategoryRequestValidator.cs` — DELETE (if it duplicates `AssignCategoryCommandValidator`)
- `src/InfoDumpManager.WebAPI/Validators/Categories/CreateCategoryRequestValidator.cs` — DELETE (if it duplicates `CreateCategoryCommandValidator`)

**Keep** validators that validate API-specific request contracts that have no command counterpart:
- `src/InfoDumpManager.WebAPI/Validators/Auth/RegisterRequestValidator.cs` — KEEP
- `src/InfoDumpManager.WebAPI/Validators/Auth/LoginRequestValidator.cs` — KEEP
- `src/InfoDumpManager.WebAPI/Validators/Categories/UpdateCategoryRequestValidator.cs` — KEEP (if no command-level equivalent)

### DI Registration Changes

In `src/InfoDumpManager.WebAPI/Program.cs`, remove:
```csharp
// REMOVE (now handled by ValidationBehavior via command validators):
services.AddValidatorsFromAssemblyContaining<CreateGemRequestValidator>();
services.AddValidatorsFromAssemblyContaining<CreateCategoryRequestValidator>();
```

Keep:
```csharp
services.AddFluentValidationAutoValidation();  // For auth request validators
services.AddValidatorsFromAssemblyContaining<CreateGEMCommandValidator>();  // Application-layer validators
services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();  // Auth validators
```

---

## 3.3 — Extract Job Status Tracking from ContentProcessingOrchestrator

### Problem
`ContentProcessingOrchestrator` manages job status via `ConcurrentDictionary` + `Channel<T>` mixed inline with agent orchestration logic.

### New File: `src/InfoDumpManager.Application/Agents/Orchestration/IJobTracker.cs`

```csharp
namespace InfoDumpManager.Application.Agents.Orchestration;

/// <summary>
/// Tracks processing job status and provides streaming updates.
/// </summary>
public interface IJobTracker
{
    void UpdateStatus(Guid jobId, ProcessingStatus status, int progress, string message);
    Task<JobStatus> GetJobStatusAsync(Guid jobId);
    IAsyncEnumerable<JobStatusUpdate> WatchJobAsync(Guid jobId);
}
```

### New File: `src/InfoDumpManager.Application/Agents/Orchestration/InMemoryJobTracker.cs`

```csharp
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace InfoDumpManager.Application.Agents.Orchestration;

/// <summary>
/// In-memory implementation of job status tracking.
/// </summary>
public sealed class InMemoryJobTracker : IJobTracker
{
    private readonly ConcurrentDictionary<Guid, JobStatus> _jobStatuses = new();
    private readonly ConcurrentDictionary<Guid, Channel<JobStatusUpdate>> _statusChannels = new();

    public void UpdateStatus(Guid jobId, ProcessingStatus status, int progress, string message)
    {
        var snapshot = new JobStatus(jobId, status, progress, message, DateTimeOffset.UtcNow);
        _jobStatuses.AddOrUpdate(jobId, snapshot, (_, _) => snapshot);

        if (_statusChannels.TryGetValue(jobId, out var channel))
        {
            channel.Writer.TryWrite(new JobStatusUpdate(jobId, status, progress, message, DateTimeOffset.UtcNow));
        }
    }

    public Task<JobStatus> GetJobStatusAsync(Guid jobId)
    {
        if (_jobStatuses.TryGetValue(jobId, out var status))
        {
            return Task.FromResult(status);
        }

        return Task.FromResult(new JobStatus(jobId, ProcessingStatus.Pending, 0, "Pending", DateTimeOffset.UtcNow));
    }

    public IAsyncEnumerable<JobStatusUpdate> WatchJobAsync(Guid jobId)
    {
        var channel = _statusChannels.GetOrAdd(jobId, _ => Channel.CreateUnbounded<JobStatusUpdate>());
        return channel.Reader.ReadAllAsync();
    }
}
```

### Refactor ContentProcessingOrchestrator

**File: `src/InfoDumpManager.Application/Agents/Orchestration/ContentProcessingOrchestrator.cs`**

1. Remove the `_jobStatuses` and `_statusChannels` fields.
2. Add `IJobTracker` to the constructor.
3. Replace all `UpdateStatus(...)` calls with `_jobTracker.UpdateStatus(...)`.
4. Delegate `GetJobStatusAsync` and `WatchJobAsync` to `_jobTracker`.

Updated constructor:
```csharp
private readonly IServiceScopeFactory _scopeFactory;
private readonly IJobTracker _jobTracker;
private readonly ILogger<ContentProcessingOrchestrator> _logger;

public ContentProcessingOrchestrator(
    IServiceScopeFactory scopeFactory,
    IJobTracker jobTracker,
    ILogger<ContentProcessingOrchestrator> logger)
{
    _scopeFactory = scopeFactory;
    _jobTracker = jobTracker;
    _logger = logger;
}
```

Updated `ProcessGEMAsync` — replace all `UpdateStatus(...)` calls:
```csharp
_jobTracker.UpdateStatus(resolvedJobId, ProcessingStatus.Processing, 0, "Starting processing");
// ... (all other UpdateStatus calls become _jobTracker.UpdateStatus)
```

Updated `GetJobStatusAsync`:
```csharp
public Task<JobStatus> GetJobStatusAsync(Guid jobId)
    => _jobTracker.GetJobStatusAsync(jobId);
```

Updated `WatchJobAsync`:
```csharp
public IAsyncEnumerable<JobStatusUpdate> WatchJobAsync(Guid jobId)
    => _jobTracker.WatchJobAsync(jobId);
```

Remove the `private void UpdateStatus(...)` method entirely.

Remove the `private ProcessingResult CreateFailedResult(...)` method's call to `UpdateStatus` — replace with `_jobTracker.UpdateStatus`.

### DI Registration

```csharp
services.AddSingleton<IJobTracker, InMemoryJobTracker>();
```

---

## 3.4 — Move BackgroundService to Infrastructure Layer

### Problem
`ContentProcessingBackgroundService` is in `src/InfoDumpManager.Application/Services/ContentProcessingBackgroundService.cs`. The Application layer should not reference `Microsoft.Extensions.Hosting`.

### Step A — Move the File

Move: `src/InfoDumpManager.Application/Services/ContentProcessingBackgroundService.cs`
To: `src/InfoDumpManager.Infrastructure/Services/ContentProcessingBackgroundService.cs`

### Step B — Update Namespace

```csharp
// BEFORE
namespace InfoDumpManager.Application.Services;

// AFTER
namespace InfoDumpManager.Infrastructure.Services;
```

### Step C — Update Usings

The file references:
- `InfoDumpManager.Application.Agents.Orchestration` — still valid (Infrastructure references Application)
- `InfoDumpManager.Application.Infrastructure.JobQueue` — still valid
- `Microsoft.Extensions.Hosting` — now appropriate in Infrastructure

### Step D — Update DI Registration

In WebAPI `Program.cs` and Web `Program.cs`:
```csharp
// BEFORE
using InfoDumpManager.Application.Services;
// AFTER
using InfoDumpManager.Infrastructure.Services;

// Registration stays the same:
services.AddHostedService<ContentProcessingBackgroundService>();
```

### Step E — Move InMemoryJobQueue to Infrastructure (Optional but Recommended)

The `InMemoryJobQueue<T>` at `src/InfoDumpManager.Application/Infrastructure/JobQueue/InMemoryJobQueue.cs` is an implementation detail. Only the interface `IJobQueue<T>` and `ProcessingJob` record belong in the Application layer.

Move: `src/InfoDumpManager.Application/Infrastructure/JobQueue/InMemoryJobQueue.cs`
To: `src/InfoDumpManager.Infrastructure/Services/InMemoryJobQueue.cs`

Update namespace:
```csharp
// BEFORE
namespace InfoDumpManager.Application.Infrastructure.JobQueue;

// AFTER
namespace InfoDumpManager.Infrastructure.Services;
```

Add the necessary usings:
```csharp
using InfoDumpManager.Application.Infrastructure.JobQueue;
```

Update DI registrations and test files to use the new namespace.

---

## Phase 3 Completion Checklist

- [x] `ValidationBehavior<TRequest, TResponse>` created in Application layer.
- [x] `ValidationBehavior` registered as `IPipelineBehavior<,>` in DI.
- [x] `ErrorHandlingMiddleware` catches `ValidationException` and returns 400.
- [x] Duplicate WebAPI validators deleted.
- [x] WebAPI validator DI registrations cleaned up.
- [x] `IJobTracker` and `InMemoryJobTracker` created.
- [x] `ContentProcessingOrchestrator` refactored to use `IJobTracker`.
- [x] `ContentProcessingBackgroundService` moved to Infrastructure.
- [x] `InMemoryJobQueue` moved to Infrastructure.
- [x] `IJobQueue<T>` and `ProcessingJob` remain in Application layer.
- [x] `dotnet build` succeeds.
- [x] `dotnet test` passes all existing tests.
