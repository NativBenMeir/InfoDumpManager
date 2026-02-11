# Phase 1: Domain Layer Fixes

status: 'Completed'

![Status: Completed](https://img.shields.io/badge/status-Completed-brightgreen)

**Goal:** Remove framework dependencies from the Domain layer, add domain event support to `AggregateRoot`, and expose shared validation constants.

**Validation:** After all changes, run `dotnet build` and `dotnet test` from the solution root. All existing tests must pass.

---

## 1.1 — Add Domain Events Collection to AggregateRoot

### Problem
`AggregateRoot<TId>` (file: `src/InfoDumpManager.Domain/Common/AggregateRoot.cs`) has no domain event support. Events are manually published by the orchestrator instead of being raised by aggregates.

### Current Code
```csharp
namespace InfoDumpManager.Domain.Common;

public abstract class AggregateRoot<TId>
{
    public TId Id { get; protected set; } = default!;
}
```

### Target Code
```csharp
using InfoDumpManager.Domain.Events;

namespace InfoDumpManager.Domain.Common;

public abstract class AggregateRoot<TId>
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public TId Id { get; protected set; } = default!;

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
```

### Notes
- `IDomainEvent` already exists at `src/InfoDumpManager.Domain/Events/GEMProcessingEvents.cs`.
- No need to add `using System.Collections.Generic;` because `<ImplicitUsings>enable</ImplicitUsings>` is set in the csproj.
- Do NOT change any entity that calls `RaiseDomainEvent` yet — that will be done in Phase 2 when the EF Core interceptor dispatches them.

---

## 1.2 — Move User Entity Out of the Domain Layer

### Problem
`User` (file: `src/InfoDumpManager.Domain/Entities/User.cs`) inherits from `IdentityUser<Guid>` which requires a `<FrameworkReference Include="Microsoft.AspNetCore.App" />` in the Domain csproj. This violates Clean Architecture.

### Step A — Create a Domain-Layer UserProfile Entity

Create a new file: `src/InfoDumpManager.Domain/Entities/UserProfile.cs`

```csharp
using System;
using InfoDumpManager.Domain.Common;

namespace InfoDumpManager.Domain.Entities;

/// <summary>
/// Domain representation of a user. Does not depend on ASP.NET Identity.
/// </summary>
public sealed class UserProfile : AggregateRoot<Guid>, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastSeenAt { get; private set; }

    private UserProfile() { }

    public static UserProfile Create(Guid tenantId, string userName, string email, string displayName)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant identifier must be provided.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(userName))
            throw new ArgumentException("Username is required.", nameof(userName));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));

        return new UserProfile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserName = userName.Trim(),
            Email = email.Trim(),
            DisplayName = displayName.Trim(),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void UpdateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name cannot be empty.", nameof(displayName));
        DisplayName = displayName.Trim();
    }

    public void SetActiveStatus(bool isActive) => IsActive = isActive;

    public void RecordActivity() => LastSeenAt = DateTimeOffset.UtcNow;
}
```

### Step B — Move the `User` Identity Entity to Infrastructure

Move file `src/InfoDumpManager.Domain/Entities/User.cs` → `src/InfoDumpManager.Infrastructure/Data/Entities/User.cs`

Change the namespace from `InfoDumpManager.Domain.Entities` to `InfoDumpManager.Infrastructure.Data.Entities`.

The class stays identical but the `using` and `namespace` change:
```csharp
using System;
using Microsoft.AspNetCore.Identity;

namespace InfoDumpManager.Infrastructure.Data.Entities;

public sealed class User : IdentityUser<Guid>
{
    // ... (identical body, no changes to members)
}
```

### Step C — Update References

1. **`src/InfoDumpManager.Infrastructure/Data/ApplicationDbContext.cs`**: Change `using InfoDumpManager.Domain.Entities;` to include `using InfoDumpManager.Infrastructure.Data.Entities;` for `User`. The `DbSet<User>` and Identity configuration remain the same because `User` is now in Infrastructure.

2. **`src/InfoDumpManager.WebAPI/Program.cs`**: Change `using InfoDumpManager.Domain.Entities;` to `using InfoDumpManager.Infrastructure.Data.Entities;` for the `AddIdentity<User, IdentityRole<Guid>>()` call.

3. **`src/InfoDumpManager.WebAPI/Services/JwtTokenService.cs`** and **`src/InfoDumpManager.WebAPI/Controllers/AuthController.cs`**: Update `using` statements if they reference `InfoDumpManager.Domain.Entities.User` to use `InfoDumpManager.Infrastructure.Data.Entities.User`.

4. **Any test files** referencing `InfoDumpManager.Domain.Entities.User`: update to `InfoDumpManager.Infrastructure.Data.Entities.User`.

### Step D — Remove ASP.NET FrameworkReference from Domain csproj

Edit `src/InfoDumpManager.Domain/InfoDumpManager.Domain.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <!-- REMOVE this entire ItemGroup: -->
  <!-- <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup> -->

</Project>
```

The `<FrameworkReference Include="Microsoft.AspNetCore.App" />` block must be deleted entirely.

### Verification
- `dotnet build src/InfoDumpManager.Domain` must succeed with zero errors.
- `dotnet build` (entire solution) must succeed.
- Any test referencing `User` must be updated and pass.

---

## 1.3 — Expose Shared Validation Constants from Domain Entities

### Problem
`MaxTitleLength = 256` is defined privately in `GEM.cs`, `CreateGEMCommandValidator.cs`, and `CreateGemRequestValidator.cs`. Drift risk.

### Changes

**File: `src/InfoDumpManager.Domain/Entities/GEM.cs`**

Change the two constants from `private` to `public`:
```csharp
// BEFORE
private const int MaxTitleLength = 256;
private const int MaxUrlLength = 2048;

// AFTER
public const int MaxTitleLength = 256;
public const int MaxUrlLength = 2048;
```

**File: `src/InfoDumpManager.Domain/Entities/Category.cs`**

Change constants from `private` to `public`:
```csharp
// BEFORE
private const int MaxNameLength = 128;
private const int MaxDescriptionLength = 512;

// AFTER
public const int MaxNameLength = 128;
public const int MaxDescriptionLength = 512;
```

**File: `src/InfoDumpManager.Domain/Entities/Tag.cs`**

Change constant from `private` to `public`:
```csharp
// BEFORE
private const int MaxNameLength = 64;

// AFTER
public const int MaxNameLength = 64;
```

**File: `src/InfoDumpManager.Application/Validators/CreateGEMCommandValidator.cs`**

Remove local constants and reference the domain entity:
```csharp
using InfoDumpManager.Domain.Entities;
// ...
public sealed class CreateGEMCommandValidator : AbstractValidator<CreateGEMCommand>
{
    // REMOVE these two lines:
    // private const int MaxTitleLength = 256;
    // private const int MaxMimeTypeLength = 64;

    public CreateGEMCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(GEM.MaxTitleLength);

        // ... rest unchanged, but use GEM.MaxUrlLength where applicable
    }
}
```

Note: `MaxMimeTypeLength = 64` doesn't exist on the domain entity. Keep it as a local constant in the validator since MIME type length is a transport concern, not a domain invariant.

**File: `src/InfoDumpManager.WebAPI/Validators/GEMs/CreateGemRequestValidator.cs`**

Same approach — remove local `MaxTitleLength`, reference `GEM.MaxTitleLength`:
```csharp
using InfoDumpManager.Domain.Entities;
// ...
public sealed class CreateGemRequestValidator : AbstractValidator<CreateGemRequest>
{
    // Keep MaxMimeTypeLength = 64 locally (transport concern)
    private const int MaxMimeTypeLength = 64;

    public CreateGemRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(GEM.MaxTitleLength);
        // ...
    }
}
```

### Verification
- `dotnet build` succeeds.
- Run: `dotnet test tests/InfoDumpManager.Tests.Unit` — all validator tests pass.

---

## Phase 1 Completion Checklist

- [x] `AggregateRoot<TId>` has `DomainEvents`, `ClearDomainEvents()`, and `RaiseDomainEvent()`.
- [x] `UserProfile` domain entity created in Domain layer.
- [x] `User` (Identity) entity moved to `Infrastructure/Data/Entities/`.
- [x] `<FrameworkReference Include="Microsoft.AspNetCore.App" />` removed from Domain csproj.
- [x] All usings updated across the solution to reference `User` from its new location.
- [x] Domain entity validation constants are `public const`.
- [x] Application and WebAPI validators reference domain constants instead of local copies.
- [x] `dotnet build` succeeds with zero errors.
- [x] `dotnet test` passes all existing tests.
