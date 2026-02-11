# Phase 2: Infrastructure Layer Fixes

status: 'Completed'

![Status: Completed](https://img.shields.io/badge/status-Completed-brightgreen)

**Goal:** Add an EF Core domain event dispatch interceptor, fix the UnitOfWork/repository DI split, and move the `IStorageService` interface to the Application layer.

**Prerequisites:** Phase 1 complete and building.

**Validation:** `dotnet build` and `dotnet test` from solution root.

---

## 2.1 — Add EF Core SaveChanges Interceptor for Domain Events

### Problem
Domain events added to `AggregateRoot.DomainEvents` (Phase 1) are never dispatched. They must be published via MediatR when `SaveChangesAsync` is called.

### New File: `src/InfoDumpManager.Infrastructure/Data/DomainEventDispatchInterceptor.cs`

```csharp
using InfoDumpManager.Application.Common.Events;
using InfoDumpManager.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace InfoDumpManager.Infrastructure.Data;

/// <summary>
/// Intercepts SaveChanges to dispatch domain events raised by aggregates.
/// </summary>
public sealed class DomainEventDispatchInterceptor : SaveChangesInterceptor
{
    private readonly IMediator _mediator;

    public DomainEventDispatchInterceptor(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            await DispatchDomainEventsAsync(eventData.Context, cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private async Task DispatchDomainEventsAsync(DbContext context, CancellationToken cancellationToken)
    {
        // Collect all aggregates with pending events.
        // Use reflection-free approach via ChangeTracker.
        var aggregatesWithEvents = context.ChangeTracker
            .Entries()
            .Where(e => e.Entity is IAggregateWithEvents awe && awe.DomainEvents.Count > 0)
            .Select(e => (IAggregateWithEvents)e.Entity)
            .ToList();

        var domainEvents = aggregatesWithEvents
            .SelectMany(a => a.DomainEvents)
            .ToList();

        // Clear events before publishing to avoid infinite loops if
        // a handler modifies an aggregate and calls SaveChanges again.
        foreach (var aggregate in aggregatesWithEvents)
        {
            aggregate.ClearDomainEvents();
        }

        foreach (var domainEvent in domainEvents)
        {
            await _mediator.Publish(new DomainEventNotification(domainEvent), cancellationToken);
        }
    }
}
```

### Required Supporting Interface

The interceptor needs a non-generic way to check for domain events. Add this interface to the Domain layer.

**New File: `src/InfoDumpManager.Domain/Common/IAggregateWithEvents.cs`**

```csharp
using InfoDumpManager.Domain.Events;

namespace InfoDumpManager.Domain.Common;

/// <summary>
/// Non-generic interface for aggregates that raise domain events.
/// Used by the infrastructure interceptor to discover pending events.
/// </summary>
public interface IAggregateWithEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}
```

### Update AggregateRoot to Implement the Interface

**File: `src/InfoDumpManager.Domain/Common/AggregateRoot.cs`**

The file was updated in Phase 1. Now additionally implement `IAggregateWithEvents`:

```csharp
using InfoDumpManager.Domain.Events;

namespace InfoDumpManager.Domain.Common;

public abstract class AggregateRoot<TId> : IAggregateWithEvents
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public TId Id { get; protected set; } = default!;

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
```

### Register the Interceptor

**File: `src/InfoDumpManager.Infrastructure/InfoDumpManager.Infrastructure.csproj`**

Add a PackageReference for MediatR (needed by the interceptor):
```xml
<PackageReference Include="MediatR" Version="14.0.0" />
```

**Registration** will happen in Phase 4 (DI extension methods), but for now note the pattern:

In any DI setup that configures `ApplicationDbContext`, the interceptor must be registered:
```csharp
services.AddScoped<DomainEventDispatchInterceptor>();

services.AddDbContext<ApplicationDbContext>((sp, options) =>
    options.UseNpgsql(dataSource, sql => { ... })
           .AddInterceptors(sp.GetRequiredService<DomainEventDispatchInterceptor>()));
```

This replaces the current registration pattern where `AddDbContext` takes no service provider parameter.

---

## 2.2 — Fix UnitOfWork / Repository DI Split-Brain

### Problem
`UnitOfWork` creates repository instances with `new GEMRepository(_context)`, bypassing DI. Meanwhile, repositories are ALSO registered separately in DI (`services.AddScoped<IGEMRepository, GEMRepository>()`). Code that injects `IGEMRepository` directly gets a different instance than `IUnitOfWork.GEMs`.

### Solution
Inject repositories into UnitOfWork via constructor. Remove standalone repository DI registrations.

### Updated File: `src/InfoDumpManager.Infrastructure/Repositories/UnitOfWork.cs`

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Infrastructure.Data;

namespace InfoDumpManager.Infrastructure.Repositories;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;

    public UnitOfWork(
        ApplicationDbContext context,
        IGEMRepository gemRepository,
        ICategoryRepository categoryRepository,
        ITagRepository tagRepository,
        ICategorySuggestionRepository categorySuggestionRepository,
        IActivityLogRepository activityLogRepository)
    {
        _context = context;
        GEMs = gemRepository;
        Categories = categoryRepository;
        Tags = tagRepository;
        CategorySuggestions = categorySuggestionRepository;
        ActivityLogs = activityLogRepository;
    }

    public IGEMRepository GEMs { get; }
    public ICategoryRepository Categories { get; }
    public ITagRepository Tags { get; }
    public ICategorySuggestionRepository CategorySuggestions { get; }
    public IActivityLogRepository ActivityLogs { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    public ValueTask DisposeAsync()
        => _context.DisposeAsync();
}
```

### DI Registration Changes

In **both** `src/InfoDumpManager.WebAPI/Program.cs` and `src/InfoDumpManager.Web/Program.cs`, **keep** the individual repository registrations (they are needed for constructor injection into UnitOfWork), and keep the UnitOfWork registration. The registrations stay the same:

```csharp
services.AddScoped<IGEMRepository, GEMRepository>();
services.AddScoped<ICategoryRepository, CategoryRepository>();
services.AddScoped<ITagRepository, TagRepository>();
services.AddScoped<ICategorySuggestionRepository, CategorySuggestionRepository>();
services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
services.AddScoped<IUnitOfWork, UnitOfWork>();
```

Note: `IActivityLogRepository` / `ActivityLogRepository` was not previously registered as a standalone service. Add the registration line.

### SummarizationAgent Fix

The `SummarizationAgent` (file: `src/InfoDumpManager.Application/Agents/Implementations/SummarizationAgent.cs`) injects `IGEMRepository` directly. After this fix, the same instance is shared. No code changes needed in the agent itself, but the DI registrations above ensure it gets the same instance.

---

## 2.3 — Move IStorageService Interface to Application Layer

### Problem
`IStorageService` is defined in `src/InfoDumpManager.Infrastructure/Services/IStorageService.cs`. The Application layer cannot depend on Infrastructure, so if application code ever needs to store/retrieve files, it has no interface to use.

### Step A — Create the Interface in Application

**New File: `src/InfoDumpManager.Application/Services/Storage/IStorageService.cs`**

```csharp
using System.Threading;
using System.Threading.Tasks;

namespace InfoDumpManager.Application.Services.Storage;

/// <summary>
/// Abstraction for object storage operations (e.g., MinIO, S3).
/// </summary>
public interface IStorageService
{
    Task<string> UploadSnapshotAsync(string objectKey, string htmlContent, string contentType, CancellationToken cancellationToken = default);
    Task<string> GetSnapshotAsync(string objectKey, CancellationToken cancellationToken = default);
}
```

### Step B — Update Infrastructure Implementation

**File: `src/InfoDumpManager.Infrastructure/Services/MinioStorageService.cs`**

Change the `using` to point to the new interface location and remove the old `IStorageService` from Infrastructure:
```csharp
// BEFORE
using InfoDumpManager.Infrastructure.Services;
// AFTER
using InfoDumpManager.Application.Services.Storage;
```

The class declaration `MinioStorageService : IStorageService` remains the same — just the namespace of `IStorageService` changes.

### Step C — Delete the Old Interface

Delete file: `src/InfoDumpManager.Infrastructure/Services/IStorageService.cs`

### Step D — Update DI Registration

In WebAPI `Program.cs` and Web `Program.cs`:
```csharp
// BEFORE
using InfoDumpManager.Infrastructure.Services;
// AFTER (add)
using InfoDumpManager.Application.Services.Storage;

// Registration stays the same:
services.AddScoped<IStorageService, MinioStorageService>();
```

Update any other files (e.g., `IWebScrapingService`) that reference `InfoDumpManager.Infrastructure.Services.IStorageService` to use `InfoDumpManager.Application.Services.Storage.IStorageService`.

---

## Phase 2 Completion Checklist

- [x] `IAggregateWithEvents` interface created in Domain.
- [x] `AggregateRoot<TId>` implements `IAggregateWithEvents`.
- [x] `DomainEventDispatchInterceptor` created in Infrastructure.
- [x] MediatR PackageReference added to Infrastructure csproj.
- [x] `UnitOfWork` constructor-injects all repositories.
- [x] `IActivityLogRepository` registration added to DI.
- [x] `IStorageService` moved from Infrastructure to Application layer.
- [x] Old `IStorageService` file deleted from Infrastructure.
- [x] `MinioStorageService` updated to use Application-layer interface.
- [x] `dotnet build` succeeds.
- [x] `dotnet test` passes all existing tests.
