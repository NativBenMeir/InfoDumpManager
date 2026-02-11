# Phase 2 — ActivityLog Demotion from AggregateRoot

## Goal
`ActivityLog` is a write-only audit record with no business invariants, no domain events, and no child entities. It should not be an `AggregateRoot`. Demote it to a plain persistence entity with an `Id` property but without aggregate ceremony (no domain events collection, no repository in `IUnitOfWork`).

## Current State

**File:** `src/InfoDumpManager.Domain/Entities/ActivityLog.cs`
```csharp
public sealed class ActivityLog : AggregateRoot<Guid>, ITenantEntity
{
    // ... properties, factory method, UpdateDescription, UpdateMetadata
}
```

**File:** `src/InfoDumpManager.Domain/Repositories/IUnitOfWork.cs`
```csharp
public interface IUnitOfWork : IAsyncDisposable
{
    IGEMRepository GEMs { get; }
    ICategoryRepository Categories { get; }
    ITagRepository Tags { get; }
    ICategorySuggestionRepository CategorySuggestions { get; }
    IActivityLogRepository ActivityLogs { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

## Changes

### 2.1 — Create `AuditEntity<TId>` base class

**New file:** `src/InfoDumpManager.Domain/Common/AuditEntity.cs`

This is a simple base class for audit/log entities that need an `Id` but not the domain events collection from `AggregateRoot`.

```csharp
namespace InfoDumpManager.Domain.Common;

/// <summary>
/// Base class for write-only audit/log entities that do not participate
/// in domain event dispatch. Use instead of AggregateRoot for simple records.
/// </summary>
public abstract class AuditEntity<TId>
{
    public TId Id { get; protected set; } = default!;
}
```

### 2.2 — Change `ActivityLog` base class

**File:** `src/InfoDumpManager.Domain/Entities/ActivityLog.cs`

Change:
```csharp
public sealed class ActivityLog : AggregateRoot<Guid>, ITenantEntity
```
To:
```csharp
public sealed class ActivityLog : AuditEntity<Guid>, ITenantEntity
```

This removes the `DomainEvents` list and the `RaiseDomainEvent` / `ClearDomainEvents` methods from `ActivityLog` — which were never used anyway.

No other changes to the file body are required (factory method, properties, update methods all stay).

### 2.3 — Keep `IActivityLogRepository` and `ActivityLogs` on `IUnitOfWork`

Despite the demotion, the `ActivityLog` entity is still persisted through EF Core and the repository pattern works fine for it. **No changes to `IUnitOfWork`, `UnitOfWork`, or `ActivityLogRepository`.**

The review's suggestion was to _consider_ moving it out of the aggregate root hierarchy. We keep the repository for convenience but drop the heavyweight base class.

### 2.4 — Verify EF Core configuration

**File:** Check that the EF Core entity configuration for `ActivityLog` does not rely on any `AggregateRoot`-specific properties.

The `DomainEventDispatchInterceptor` filters on `IAggregateWithEvents`. Since `AuditEntity` does **not** implement `IAggregateWithEvents`, the interceptor will simply skip `ActivityLog` entries — which is correct behavior.

EF Core maps `ActivityLog` by its `DbSet<ActivityLog>` in `ApplicationDbContext`. The `Id` property is still present on `AuditEntity<TId>`, so the primary key mapping is unchanged.

## Verification

```bash
dotnet build
dotnet test
```

Confirm `ActivityLog` no longer carries a `DomainEvents` property:
```bash
grep -n "DomainEvents" src/InfoDumpManager.Domain/Entities/ActivityLog.cs
# Should have zero matches
```
