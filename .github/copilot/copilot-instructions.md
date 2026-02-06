# GitHub Copilot Instructions for InfoDumpManager

This document provides essential guidance for GitHub Copilot to ensure all generated code is consistent with the InfoDumpManager project's architecture, technology versions, coding standards, and established patterns.

## Priority Guidelines

When generating code for the InfoDumpManager repository, follow these priorities in order:

1. **Version Compatibility**: Always detect and respect the exact versions of .NET, frameworks, and libraries specified in the project files
2. **Architecture Adherence**: Maintain the Clean Architecture pattern with strict layer boundaries and dependency flows
3. **Established Patterns**: Follow coding patterns, naming conventions, and organizational structures evident in the existing codebase
4. **Code Quality**: Prioritize maintainability, testability, and security in all generated code
5. **Documentation**: Include XML documentation comments for all public API methods and types

## Technology Stack & Key Versions

**See AGENTS.md for complete setup instructions. Reference existing .csproj files for exact package versions.**

### Critical Technologies
- **.NET**: 8.0 (Target: `net8.0`, C# 12.0, Nullable enabled, Implicit usings enabled)
- **PostgreSQL**: 16.11 with pgvector extension
- **Entity Framework Core**: 8.0.23 with Npgsql provider 9.0.1
- **MediatR**: 14.0.0 (CQRS pattern)
- **FluentValidation**: 12.1.1 (Command/Query validation)
- **AutoMapper**: 12.0.1 (Entity-to-DTO mapping)
- **Polly**: 8.6.5 (Resilience patterns)
- **Serilog**: 4.3.0 (Structured logging)
- **Semantic Kernel**: 1.70.0 (LLM orchestration)
- **xUnit**: 2.5.3 with FluentAssertions 8.8.0, Moq 4.20.72, Testcontainers 4.10.0

### Key Patterns
- Always use async/await for I/O operations (`.ToListAsync()`, `.SaveChangesAsync()`)
- MediatR: Commands/Queries inherit `IRequest<TResponse>`, Handlers implement `IRequestHandler<TRequest, TResponse>`
- FluentValidation: Validators inherit `AbstractValidator<TCommand>`
- EF Core: DbContext with explicit entity configurations, `.AsNoTracking()` for read-only queries
- Testing: `[Fact]` for simple tests, `[Theory]` with `[InlineData]` for parameterized tests

## Architecture & Project Organization

**See AGENTS.md for complete project structure details.**

### Clean Architecture Layers & Dependency Flow
- **Domain** ← Application ← Infrastructure ← API/Web
- Domain layer: **No external dependencies** (except System.*)
- Application layer: Depends on Domain only
- Infrastructure layer: Depends on Domain and Application; implements interfaces
- API/Web layers: Depend on all layers via dependency injection

### Key Architectural Principles
1. **Aggregate Roots**: Use `AggregateRoot<TId>` base class for entities (e.g., `GEM : AggregateRoot<Guid>`)
2. **Value Objects**: Use `ValueObject` base class for immutable values (e.g., `GEMSource`, `GEMSnapshot`)
3. **Repository Pattern**: Always implement repository interfaces in Domain; instances in Infrastructure
4. **CQRS**: Separate commands (write) from queries (read) using MediatR
5. **Dependency Injection**: Use ASP.NET Core's built-in DI; configure in `Program.cs`
6. **Multi-tenancy**: Implement `ITenantEntity` interface for tenant-aware entities
7. **Soft Delete**: Use `IsDeleted` flags instead of hard deletes when applicable

## Code Patterns & Conventions

### Naming Conventions
- **Classes/Interfaces**: PascalCase (e.g., `CreateGEMCommand`, `IEmbeddingService`)
- **Properties/Methods**: PascalCase (e.g., `Title`, `GetCategories()`)
- **Local Variables/Parameters**: camelCase (e.g., `gemId`, `sourceName`)
- **Constants**: PascalCase (e.g., `MaxTitleLength`)
- **Private Fields**: camelCase with `_` prefix if needed
- **Enum Values**: PascalCase (e.g., `AgentCapability.Categorization`)

### Class & File Organization
```csharp
// Domain Entity Example Pattern
public sealed class GEM : AggregateRoot<Guid>, ITenantEntity
{
    // Constants first
    private const int MaxTitleLength = 256;

    // Properties next (public auto-properties for simple types)
    public Guid TenantId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    
    // Relationships
    public Guid? CategoryId { get; private set; }
    public Category? Category { get; private set; }

    // Private constructor (for EF Core)
    private GEM() { }

    // Static factory method
    public static GEM Create(
        Guid tenantId,
        string title,
        string url,
        GEMSource source,
        GEMSnapshot snapshot,
        GEMSummary? summary = null)
    {
        // Validation
        ValidateTenant(tenantId);
        
        // Create and return
        return new GEM 
        { 
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = NormalizeTitle(title),
            // ...
        };
    }

    // Instance methods
    public void AssignCategory(Guid categoryId)
    {
        // Implementation
    }

    // Validation methods
    private static void ValidateTenant(Guid tenantId) { }
}
```

### Command Handler Pattern
```csharp
public sealed class CreateGEMCommandHandler : IRequestHandler<CreateGEMCommand, GEMDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public CreateGEMCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<GEMDto> Handle(
        CreateGEMCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Business logic using domain entities
        var gem = GEM.Create(
            request.TenantId,
            request.Title,
            request.Url,
            source,
            snapshot);

        await _unitOfWork.GEMs.AddAsync(gem, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<GEMDto>(gem);
    }
}
```

### FluentValidation Pattern
```csharp
public sealed class CreateGEMCommandValidator : AbstractValidator<CreateGEMCommand>
{
    public CreateGEMCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(256).WithMessage("Title cannot exceed 256 characters");

        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("URL is required")
            .Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out _))
            .WithMessage("URL must be a valid URI");
    }
}
```

### Async/Await Pattern
- Use `async` and `await` for all I/O operations (database, HTTP, file)
- Method names should end with `Async` (e.g., `GetGEMAsync()`, `SaveChangesAsync()`)
- Use `.ConfigureAwait(false)` in library code (optional in ASP.NET Core controllers)
- Example: `var entity = await _repository.GetByIdAsync(id, cancellationToken);`

### Error Handling Pattern
```csharp
try
{
    // Operation
    var result = await _service.ProcessAsync(input, cancellationToken);
    Log.Information("Process completed: {@Result}", result);
    return result;
}
catch (ArgumentNullException ex)
{
    Log.Warning(ex, "Invalid argument provided");
    throw;
}
catch (InvalidOperationException ex)
{
    Log.Error(ex, "Operation failed with business logic error");
    throw;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unexpected error occurred");
    throw;
}
```

### Logging Pattern (Serilog)
```csharp
// Structured logging with properties
Log.Information("GEM created: {@GEM}", gem);
Log.Warning("Web scraping timeout for URL: {Url}", url);
Log.Error(ex, "Database operation failed for entity {EntityId}", entityId);

// Context-based enrichment
using (LogContext.PushProperty("TenantId", tenantId))
{
    // All logs within scope will include TenantId
    await _service.ProcessAsync(input);
}
```

### Entity Framework Core Pattern
```csharp
// Entity configuration
public sealed class GEMConfiguration : IEntityTypeConfiguration<GEM>
{
    public void Configure(EntityTypeBuilder<GEM> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
        
        // Complex property (value object)
        builder.OwnsOne(x => x.Source, nav =>
        {
            nav.Property(s => s.Url).HasMaxLength(2048);
        });

        // Foreign key
        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
```

## Code Quality Standards

### Maintainability
- Write self-documenting code with clear, descriptive naming
- Follow the DRY principle (Don't Repeat Yourself)
- Keep methods focused on single responsibilities (max 15-20 lines typical)
- Use LINQ efficiently: `.AsNoTracking()` for read-only queries

### Performance
- Use `.AsNoTracking()` for EF Core queries that don't need updates
- Implement pagination for large data sets
- Cache frequently accessed data (Redis is configured)
- Batch database operations when possible

### Security
- Always use parameterized queries (EF Core handles this automatically)
- Validate all user input via FluentValidation
- Use `ArgumentNullException.ThrowIfNull()` for parameter validation
- Use soft deletes to prevent accidental data loss
- Sanitize HTML content from web scraping

### Testability
- Depend on abstractions (interfaces), not concrete implementations
- Keep constructors simple; use dependency injection
- Use factories for complex object creation
- Minimize coupling between classes

## Documentation Requirements

### XML Documentation
- Document all **public** types and methods with `/// <summary>` comments
- Use `<param>`, `<returns>`, `<exception>` tags for method documentation
- Example:
  ```csharp
  /// <summary>
  /// Creates a new GEM aggregate with validation.
  /// </summary>
  /// <param name="tenantId">The tenant identifier.</param>
  /// <param name="title">The GEM title (max 256 characters).</param>
  /// <returns>A newly created GEM instance.</returns>
  /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
  public static GEM Create(Guid tenantId, string title, ...)
  ```

### Inline Comments
- Use sparingly; let code be self-documenting
- Explain *why*, not *what* (code shows what)
- Document non-obvious business logic

## Common Patterns to Avoid

❌ **Avoid**:
- Passing entities between layers; use DTOs
- Business logic in controllers; use application layer
- Tight coupling to infrastructure (EF Core in domain)
- Synchronous database calls; use async
- Generic catch-all exception handlers
- Leaving TODO comments; create issues or implement

✅ **Instead**:
- Use repository and unit of work patterns
- Keep domain layer free of infrastructure dependencies
- Use dependency injection for loose coupling
- Always use `async/await` for I/O
- Catch specific exceptions
- Document via code and tests

## Related Documentation

- **Project Setup**: See [AGENTS.md](../../AGENTS.md) for setup and running instructions
- **API Specifications**: See [docs/api.md](../../docs/api.md)
- **Database Migrations**: Run `dotnet ef migrations list` to see applied migrations

## General Best Practices

- Follow the patterns and conventions evident in existing code
- When in doubt, scan similar files in the codebase for reference
- Maintain consistency with established patterns over external best practices
- Always write tests (aim for 80%+ coverage)
- Keep commits small and focused
- Use meaningful commit messages with conventional format
- Request code review before merging significant changes
- Never commit secrets or configuration credentials

---

**Last Updated**: February 4, 2026  
**Version**: 2.0 (Streamlined)  
**Maintainers**: Development Team
