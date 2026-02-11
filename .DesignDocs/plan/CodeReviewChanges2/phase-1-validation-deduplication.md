# Phase 1 — Validation Deduplication

## Goal
Eliminate duplicated max-length constants and validation rules across layers.
Domain entity constants become the single source of truth.
Application-layer `CreateGEMCommandValidator` already references `GEM.MaxTitleLength` and `GEM.MaxUrlLength`, so that is done.
The remaining issue is `CreateCategoryCommandValidator` and `UpdateCategoryRequestValidator`, which both declare their own `MaxNameLength = 128` and `MaxDescriptionLength = 512` instead of referencing `Category.MaxNameLength` / `Category.MaxDescriptionLength`.

## Current State

### Domain entity (correct — source of truth)
**File:** `src/InfoDumpManager.Domain/Entities/Category.cs`
```csharp
public sealed class Category : AggregateRoot<Guid>, ITenantEntity
{
    public const int MaxNameLength = 128;
    public const int MaxDescriptionLength = 512;
    // ...
}
```

### Application validator (duplicated constants)
**File:** `src/InfoDumpManager.Application/Validators/CreateCategoryCommandValidator.cs`
```csharp
public sealed class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    private const int MaxNameLength = 128;       // ← duplicated
    private const int MaxDescriptionLength = 512; // ← duplicated
    // ...
}
```

### API validator (duplicated constants)
**File:** `src/InfoDumpManager.WebAPI/Validators/Categories/UpdateCategoryRequestValidator.cs`
```csharp
public sealed class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    private const int MaxNameLength = 128;       // ← duplicated
    private const int MaxDescriptionLength = 512; // ← duplicated
    // ...
}
```

## Changes

### 1.1 — Update `CreateCategoryCommandValidator`

**File:** `src/InfoDumpManager.Application/Validators/CreateCategoryCommandValidator.cs`

- Remove the two `private const` fields.
- Add `using InfoDumpManager.Domain.Entities;` if not present.
- Reference `Category.MaxNameLength` and `Category.MaxDescriptionLength` inline.

**Result:**
```csharp
using FluentValidation;
using InfoDumpManager.Application.Categories.Commands;
using InfoDumpManager.Domain.Entities;

namespace InfoDumpManager.Application.Validators;

public sealed class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(Category.MaxNameLength);

        RuleFor(x => x.Description)
            .MaximumLength(Category.MaxDescriptionLength)
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
    }
}
```

### 1.2 — Update `UpdateCategoryRequestValidator`

**File:** `src/InfoDumpManager.WebAPI/Validators/Categories/UpdateCategoryRequestValidator.cs`

- Remove the two `private const` fields.
- Add `using InfoDumpManager.Domain.Entities;` if not present.
- Reference `Category.MaxNameLength` and `Category.MaxDescriptionLength`.

**Result:**
```csharp
using FluentValidation;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.WebAPI.Contracts.Categories;

namespace InfoDumpManager.WebAPI.Validators.Categories;

public sealed class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(Category.MaxNameLength);

        RuleFor(x => x.Description)
            .MaximumLength(Category.MaxDescriptionLength)
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
    }
}
```

### 1.3 — Remove the API-layer GEM request validator (it no longer exists)

The original review noted a `CreateGemRequestValidator` at the WebAPI layer. This file has already been deleted (`src/InfoDumpManager.WebAPI/Validators/GEMs/` is empty). **No action required.**

The application-layer `CreateGEMCommandValidator` already uses `GEM.MaxTitleLength` and `GEM.MaxUrlLength` from the domain entity. Combined with the `ValidationBehavior<TRequest, TResponse>` MediatR pipeline, this means validation runs automatically for all entry points (API, Web, background). **No action required.**

## Verification

```bash
dotnet build
dotnet test tests/InfoDumpManager.Tests.Unit --filter "Category"
```

Ensure no hard-coded `128` or `512` constants remain in any validator file:
```bash
grep -rn "private const int Max" src/InfoDumpManager.Application/Validators/
grep -rn "private const int Max" src/InfoDumpManager.WebAPI/Validators/Categories/
```
