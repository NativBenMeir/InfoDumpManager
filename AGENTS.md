# AGENTS.md

## Project Overview

**InfoDumpManager** (GEM System) is a .NET 8-based enterprise application for ingesting web content, generating AI-powered summaries, intelligently categorizing information, applying semantic tags, and providing natural language Q&A over a knowledge base.

**Architecture**: Modular monolith with Domain-Driven Design (DDD), CQRS-lite pattern, and containerized deployment via Docker.

**Key Technologies**:
- **.NET 8.0 LTS** - Framework
- **ASP.NET Core** - Web API and Razor Pages UI
- **Entity Framework Core 8.0** - ORM
- **PostgreSQL 16 + pgvector** - Data store with vector search
- **Redis** - Distributed caching
- **MinIO** - Object storage for web snapshots
- **Docker Compose** - Multi-container orchestration
- **MediatR** - CQRS pattern
- **FluentValidation** - Input validation
- **Serilog** - Structured logging
- **Playwright** - Web scraping
- **OpenAI / Azure OpenAI** - LLM integration
- **Semantic Kernel** - LLM orchestration

**Project Structure**:
```
src/
  InfoDumpManager.Domain/              # DDD entities, aggregates, value objects, interfaces
  InfoDumpManager.Application/         # Commands, queries, handlers, DTOs, validators
  InfoDumpManager.Infrastructure/      # Repositories, services, EF Core, background jobs
  InfoDumpManager.WebAPI/              # ASP.NET Core Web API controllers, middleware
  InfoDumpManager.Web/                 # ASP.NET Core Razor Pages web application
tests/
  InfoDumpManager.Tests.Unit/          # Unit tests (xUnit, Moq)
  InfoDumpManager.Tests.Integration/   # Integration tests (Testcontainers)
docker-compose.yml                     # Development container setup
```

**Phases**: 4-phase delivery spanning 13-17 weeks (see `docs/ways-of-work/plan/gem-ingestion-categorization/implementation-plan-1.md`)

---

## Setup Commands

### Prerequisites
- **.NET 8.0 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Docker Desktop** - [Download](https://www.docker.com/products/docker-desktop)
- **Git** - [Download](https://git-scm.com/)
- **IDE**: Visual Studio 2022, JetBrains Rider, or VS Code with C# extensions

### Initial Setup

```bash
# Clone repository
git clone <repository-url>
cd InfoDumpManager

# Restore NuGet dependencies
dotnet restore

# Build solution
dotnet build

# Start Docker containers (PostgreSQL 16, pgvector, Redis, MinIO)
docker-compose up -d

# Apply database migrations (creates schema and initial data)
dotnet ef database update --project src/InfoDumpManager.Infrastructure --startup-project src/InfoDumpManager.WebAPI

# Seed development data
dotnet run --project src/InfoDumpManager.WebAPI -- --seed-data
```

### Environment Configuration

```bash
# Copy environment template
cp .env.template .env

# Edit .env with your values:
# - LLM_PROVIDER (OpenAI or AzureOpenAI)
# - OPENAI_API_KEY or AZURE_OPENAI_KEY
# - OPENAI_MODEL (e.g., gpt-4, gpt-3.5-turbo)
# - DATABASE_CONNECTION_STRING
# - REDIS_CONNECTION_STRING
# - MINIO_ENDPOINT, MINIO_ACCESS_KEY, MINIO_SECRET_KEY
# - JWT_SECRET_KEY
```

### Verify Setup

```bash
# Check .NET SDK version
dotnet --version

# Verify Docker containers are running
docker ps

# Test API connectivity
curl http://localhost:5000/health

# Test database connectivity
dotnet ef dbcontext info --project src/InfoDumpManager.Infrastructure

# View Docker logs
docker-compose logs -f
```

---

## Development Workflow

### Start Development Services

```bash
# Terminal 1: Start Docker containers
docker-compose up -d

# Terminal 2: Start Web API (ASP.NET Core)
dotnet run --project src/InfoDumpManager.WebAPI
# Runs on: http://localhost:5000
# Swagger UI: http://localhost:5000/swagger/index.html

# Terminal 3: Start Web UI (Razor Pages)
dotnet run --project src/InfoDumpManager.Web
# Runs on: http://localhost:5001
```

### Hot Reload Development

```bash
# Watch mode with automatic restart on code changes
dotnet watch run --project src/InfoDumpManager.WebAPI

# Verify watch is working: change a controller, should auto-reload
```

### Access Development Services

- **Web UI**: http://localhost:5001
- **Web API**: http://localhost:5000
- **Swagger API Docs**: http://localhost:5000/swagger/index.html
- **PostgreSQL**: localhost:5432 (user: postgres, password: postgres)
- **Redis**: localhost:6379
- **MinIO Console**: http://localhost:9001 (user: minioadmin, password: minioadmin)

### Database Management

```bash
# Create new migration
dotnet ef migrations add <MigrationName> --project src/InfoDumpManager.Infrastructure --startup-project src/InfoDumpManager.WebAPI

# Apply migrations
dotnet ef database update --project src/InfoDumpManager.Infrastructure --startup-project src/InfoDumpManager.WebAPI

# Revert to previous migration
dotnet ef database update <PreviousMigrationName> --project src/InfoDumpManager.Infrastructure --startup-project src/InfoDumpManager.WebAPI

# Remove last migration
dotnet ef migrations remove --project src/InfoDumpManager.Infrastructure --startup-project src/InfoDumpManager.WebAPI

# View migration script (SQL)
dotnet ef migrations script <FromMigration> <ToMigration> --project src/InfoDumpManager.Infrastructure --startup-project src/InfoDumpManager.WebAPI
```

### Code Generation

```bash
# Generate OpenAPI client from Swagger
dotnet tool install -g NSwag.ConsoleCore
nswag openapi2csharp /input:http://localhost:5000/swagger/v1/swagger.json /output:Generated/OpenAPI.cs

# Generate AutoMapper mappings (verify configuration)
dotnet run --project src/InfoDumpManager.WebAPI -- --validate-mappings
```

---

## Testing Instructions

### Run All Tests

```bash
# Run all unit and integration tests
dotnet test

# Run tests with verbose output
dotnet test --verbosity detailed

# Run tests with code coverage
dotnet test /p:CollectCoverage=true /p:CoverageFormat=opencover /p:CoverageDirectory=./coverage

# View coverage report (requires ReportGenerator)
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"./coverage/coverage.opencover.xml" -targetdir:"./coverage/report"
```

### Run Specific Tests

```bash
# Run tests for specific project
dotnet test tests/InfoDumpManager.Tests.Unit

# Run tests matching pattern
dotnet test --filter "FullyQualifiedName~CreateGEMCommand"

# Run single test class
dotnet test --filter "ClassName=InfoDumpManager.Tests.Unit.Application.GEMs.Commands.CreateGEMCommandHandlerTests"

# Run with specific logger
dotnet test --logger "console;verbosity=detailed"
```

### Unit Tests

**Framework**: xUnit + FluentAssertions + Moq

**Location**: `tests/InfoDumpManager.Tests.Unit/`

**Naming Convention**: `[FeatureName][ComponentName]Tests.cs`

```bash
# Run only unit tests
dotnet test tests/InfoDumpManager.Tests.Unit

# Run unit tests with watch mode
dotnet watch test --project tests/InfoDumpManager.Tests.Unit

# Target: 80%+ code coverage for domain and application logic
dotnet test tests/InfoDumpManager.Tests.Unit /p:CollectCoverage=true
```

**Test Patterns**:
- **Arrange-Act-Assert (AAA)**: Setup → Execute → Verify
- **Naming**: `[Method]_[Scenario]_[ExpectedResult]`
- **Mocking**: Use Moq for external dependencies (repositories, services)
- **Example**:
  ```csharp
  [Fact]
  public void CreateGEM_WithValidUrl_ReturnsGEMWithSourceLink()
  {
    // Arrange
    var command = new CreateGEMCommand("https://example.com");
    var mockRepository = new Mock<IGEMRepository>();
    var handler = new CreateGEMCommandHandler(mockRepository.Object);
    
    // Act
    var result = await handler.Handle(command, CancellationToken.None);
    
    // Assert
    result.Should().NotBeNull();
    result.Source.Url.Should().Be("https://example.com");
  }
  ```

### Integration Tests

**Framework**: Testcontainers + xUnit + FluentAssertions

**Location**: `tests/InfoDumpManager.Tests.Integration/`

**Prerequisites**: Docker must be running

```bash
# Run only integration tests (slower, requires Docker)
dotnet test tests/InfoDumpManager.Tests.Integration

# Run integration tests with watch mode
dotnet watch test --project tests/InfoDumpManager.Tests.Integration

# Run specific integration test
dotnet test tests/InfoDumpManager.Tests.Integration --filter "FullyQualifiedName~GEMRepositoryTests"
```

**Test Patterns**:
- **Database Tests**: Use Testcontainers PostgreSQL
- **API Tests**: Use WebApplicationFactory for in-memory API testing
- **Naming**: `[Repository/Service]Tests.cs`
- **Example**:
  ```csharp
  [Fact]
  public async Task CreateGEM_WithValidData_PersistsToDatabase()
  {
    // Arrange - Testcontainer setup happens in fixture
    var gem = GEM.Create(new GEMSource("https://example.com"), "Title", "Summary");
    
    // Act
    await _repository.AddAsync(gem);
    await _unitOfWork.SaveChangesAsync();
    
    // Assert
    var retrieved = await _repository.GetByIdAsync(gem.Id);
    retrieved.Should().NotBeNull();
    retrieved.Source.Url.Should().Be("https://example.com");
  }
  ```

### Test Organization

```
tests/
  InfoDumpManager.Tests.Unit/
    Domain/                    # Entity and value object tests
    Application/
      GEMs/
        Commands/              # Command handler tests
        Queries/               # Query handler tests
      Categories/
        Commands/
        Queries/
    Infrastructure/            # Service tests with mocks
  InfoDumpManager.Tests.Integration/
    Data/                      # Repository and DbContext tests
    API/                       # API controller endpoint tests
    Fixtures/                  # Testcontainer fixtures and test data
```

---

## Code Style & Conventions

### Language & Framework Standards

- **C# 12.0** features (required by .NET 8)
- **nullable reference types** enabled (`#nullable enable`)
- **async/await** for all I/O operations
- **LINQ** preferred over loops

### Architecture Layers

**Domain Layer** (`InfoDumpManager.Domain/`):
- Pure business logic with no external dependencies
- Entities, Value Objects, Aggregates
- Repository and Service interfaces (no implementations)
- No references to EF Core, ASP.NET, or external libraries
- Exception types for domain errors

**Application Layer** (`InfoDumpManager.Application/`):
- MediatR Commands and Queries
- Command/Query Handlers (business orchestration)
- DTOs for API contracts
- FluentValidation validators
- AutoMapper profiles for entity-to-DTO mappings
- Service interfaces (business logic, not infrastructure)

**Infrastructure Layer** (`InfoDumpManager.Infrastructure/`):
- EF Core repositories implementing domain interfaces
- Unit of Work pattern for transaction management
- External service implementations (LLM, web scraping, storage)
- Background services (IHostedService, BackgroundService)
- Database migrations and configurations

**API/Web Layers** (`InfoDumpManager.WebAPI/`, `InfoDumpManager.Web/`):
- ASP.NET Core controllers with minimal logic
- Dependency injection configuration
- Middleware for cross-cutting concerns (logging, error handling)
- View models for UI (distinct from DTOs)

### Design Patterns

```csharp
// ✅ CQRS Pattern with MediatR
public record CreateGEMCommand(string Url) : IRequest<GEMDto>;

// ✅ Repository Pattern
public interface IGEMRepository
{
    Task<GEM?> GetByIdAsync(GEMId id, CancellationToken ct = default);
    Task AddAsync(GEM gem, CancellationToken ct = default);
}

// ✅ Value Objects (immutable, entity equality by value)
public sealed record GEMSource(string Url)
{
    public GEMSource(string url) : this(Uri.TryCreate(url, UriKind.Absolute, out _) 
        ? url 
        : throw new ArgumentException("Invalid URL", nameof(url)))
    {
    }
}

// ✅ Aggregate Root (encapsulates entities and value objects)
public class GEM : Entity<GEMId>, IAggregateRoot
{
    public GEMSource Source { get; }
    public string Title { get; private set; }
    public GEMSummary? Summary { get; private set; }
    
    public static GEM Create(GEMSource source, string title, string? summary = null)
    {
        // Factory method with validation
        var gem = new GEM { Source = source, Title = title };
        gem.AddDomainEvent(new GEMCreatedEvent(gem.Id));
        return gem;
    }
}

// ✅ Fluent Validation
public class CreateGEMCommandValidator : AbstractValidator<CreateGEMCommand>
{
    public CreateGEMCommandValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty()
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("Invalid URL format");
    }
}

// ✅ Strategy Pattern for LLM Providers
public interface ILLMProvider
{
    Task<string> GenerateCompletionAsync(string prompt, CancellationToken ct);
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct);
}

// ✅ Dependency Injection
services.AddScoped<IGEMRepository, GEMRepository>();
services.AddScoped<ILLMProvider>(sp => 
    Environment.GetEnvironmentVariable("LLM_PROVIDER") == "Azure"
        ? new AzureOpenAILLMProvider(...)
        : new OpenAILLMProvider(...));
```

### Naming Conventions

| Concept | Convention | Example |
|---------|-----------|---------|
| **Classes** | PascalCase | `GEMRepository`, `CreateGEMCommand` |
| **Interfaces** | IPascalCase | `IGEMRepository`, `ILLMProvider` |
| **Methods** | PascalCase, verb-first | `GetByIdAsync`, `AddAsync`, `CreateGEM` |
| **Properties** | PascalCase | `Title`, `Summary`, `Source` |
| **Private fields** | _camelCase | `_repository`, `_logger` |
| **Local variables** | camelCase | `gem`, `categories` |
| **Constants** | UPPER_SNAKE_CASE | `MAX_RETRIES`, `DEFAULT_TIMEOUT` |
| **Async methods** | Suffix `Async` | `GetByIdAsync`, `CreateAsync` |
| **Events** | Suffix `Event` | `GEMCreatedEvent`, `SummaryGeneratedEvent` |
| **Commands** | Suffix `Command` | `CreateGEMCommand`, `AssignCategoryCommand` |
| **Queries** | Suffix `Query` | `GetGEMByIdQuery`, `SearchGEMsQuery` |
| **Handlers** | Suffix `Handler` | `CreateGEMCommandHandler`, `GetGEMByIdQueryHandler` |
| **Services** | Suffix `Service` | `WebScrapingService`, `LLMOrchestrationService` |

### File Organization

```
src/InfoDumpManager.Domain/
├── Entities/
│   ├── GEM.cs
│   ├── Category.cs
│   ├── Tag.cs
│   └── User.cs
├── ValueObjects/
│   ├── GEMSource.cs
│   ├── GEMSnapshot.cs
│   └── GEMSummary.cs
├── Repositories/
│   ├── IGEMRepository.cs
│   └── ICategoryRepository.cs
├── Services/
│   └── ILLMProvider.cs
├── Events/
│   ├── GEMCreatedEvent.cs
│   └── DomainEvent.cs
└── Exceptions/
    ├── DomainException.cs
    └── GEMNotFoundException.cs

src/InfoDumpManager.Application/
├── GEMs/
│   ├── Commands/
│   │   ├── CreateGEMCommand.cs
│   │   └── CreateGEMCommandHandler.cs
│   ├── Queries/
│   │   ├── GetGEMByIdQuery.cs
│   │   └── GetGEMByIdQueryHandler.cs
│   ├── DTOs/
│   │   └── GEMDto.cs
│   ├── Validators/
│   │   └── CreateGEMCommandValidator.cs
│   └── Mappings/
│       └── GEMMappingProfile.cs
├── Categories/
│   ├── Commands/
│   ├── Queries/
│   ├── DTOs/
│   └── Validators/
└── Common/
    ├── Interfaces/
    └── Behaviours/
```

### Import Organization

```csharp
// 1. System namespaces
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

// 2. External library namespaces
using MediatR;
using FluentValidation;

// 3. Project namespaces
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Repositories;

// 4. Relative project namespaces
using InfoDumpManager.Application.Common;
```

### Code Review Checklist

- [ ] Follows DDD principles (domain logic encapsulated)
- [ ] No circular dependencies between layers
- [ ] All public methods are async where applicable
- [ ] FluentValidation used for all input validation
- [ ] Proper null handling with nullable reference types
- [ ] Serilog ILogger injected, not Console.WriteLine
- [ ] Unit tests written for domain and application logic
- [ ] Integration tests for data access and API layers
- [ ] No hardcoded configuration (use appsettings.json or environment variables)
- [ ] Repository pattern used for data access
- [ ] MediatR for CQRS pattern implementation
- [ ] No SQL in controllers/handlers (use repositories)
- [ ] Proper error handling with domain exceptions
- [ ] Comments for complex business logic, not obvious code

---

## Build and Deployment

### Local Build

```bash
# Build solution
dotnet build

# Build in Release mode (optimized)
dotnet build --configuration Release

# Build specific project
dotnet build src/InfoDumpManager.WebAPI --configuration Release

# Publish for self-contained deployment
dotnet publish src/InfoDumpManager.WebAPI -c Release -o ./publish/webapi
dotnet publish src/InfoDumpManager.Web -c Release -o ./publish/web
```

### Docker Build

```bash
# Build Docker images
docker build -f Dockerfile.webapi -t infodumpmanager-webapi:latest .
docker build -f Dockerfile.web -t infodumpmanager-web:latest .

# Push to registry
docker tag infodumpmanager-webapi:latest <registry>/infodumpmanager-webapi:1.0.0
docker push <registry>/infodumpmanager-webapi:1.0.0

# Run containers
docker-compose -f docker-compose.prod.yml up -d
```

### Environment-Specific Builds

```bash
# Development (debug symbols, verbose logging)
dotnet build --configuration Debug

# Staging (optimized but with diagnostics)
dotnet build --configuration Release
# Enable: detailed logging, test endpoints

# Production (fully optimized)
dotnet build --configuration Release
# Disable: debug endpoints, test data seeding
```

### Build Output Structure

```
publish/
├── webapi/
│   ├── InfoDumpManager.WebAPI
│   ├── InfoDumpManager.WebAPI.dll
│   ├── InfoDumpManager.WebAPI.exe (Windows only)
│   ├── appsettings.json
│   ├── appsettings.Production.json
│   └── wwwroot/
└── web/
    ├── InfoDumpManager.Web
    ├── InfoDumpManager.Web.dll
    └── appsettings.json
```

### Deployment Checklist

- [ ] All unit tests pass: `dotnet test`
- [ ] All integration tests pass: `dotnet test tests/InfoDumpManager.Tests.Integration`
- [ ] No build warnings: `dotnet build --no-warn`
- [ ] Code coverage meets target (80%+)
- [ ] Database migrations run successfully in staging
- [ ] Environment variables configured (`.env` file)
- [ ] Docker images built and tested locally
- [ ] Health checks respond (GET /health)
- [ ] API documentation generated (Swagger)
- [ ] Secrets not hardcoded (review appsettings.json)
- [ ] HTTPS/TLS configured for production
- [ ] Backup strategy verified before production
- [ ] Rollback procedure documented and tested

---

## Monorepo Instructions

### Multi-Project Organization

This is a **modular monolith** (not a microservices architecture):
- Single Git repository
- Multiple .NET projects with clear separation of concerns
- Deployed as containerized services (can scale independently)
- Shared database (PostgreSQL with row-level security for multi-tenancy)

### Building Individual Projects

```bash
# Build only Web API
dotnet build src/InfoDumpManager.WebAPI

# Build only tests
dotnet build tests/

# Build Domain and Application (no dependencies on Infrastructure/Web)
dotnet build src/InfoDumpManager.Domain src/InfoDumpManager.Application
```

### Running Specific Services

```bash
# Run only Web API
dotnet run --project src/InfoDumpManager.WebAPI

# Run only Web UI
dotnet run --project src/InfoDumpManager.Web

# Run only background job processor
dotnet run --project src/InfoDumpManager.Infrastructure -- --mode=background-jobs
```

### Cross-Project Dependencies

**Dependency Graph** (→ means "depends on"):
```
WebAPI → Application → Domain
Web → Application → Domain
Infrastructure → Domain
WebAPI → Infrastructure
Web → Infrastructure
Tests.Unit → Domain, Application
Tests.Integration → Infrastructure, WebAPI, Web
```

**NEVER** add circular dependencies:
- ✅ Application → Domain (allowed, clean dependency)
- ✅ Infrastructure → Domain (allowed, implements interfaces)
- ❌ Domain → Application (breaks layering)
- ❌ Domain → Infrastructure (breaks layering)

### Adding New Projects

```bash
# Create new class library project
dotnet new classlib -n "InfoDumpManager.NewFeature" -f net8.0

# Add to solution
dotnet sln add src/InfoDumpManager.NewFeature/InfoDumpManager.NewFeature.csproj

# Add project reference
dotnet add src/InfoDumpManager.WebAPI reference src/InfoDumpManager.NewFeature

# Restore and build
dotnet restore
dotnet build
```

### Package Management

```bash
# Add NuGet package to project
dotnet add src/InfoDumpManager.Application package FluentValidation

# Update package
dotnet add src/InfoDumpManager.Application package FluentValidation --version 11.8.0

# List installed packages
dotnet list src/InfoDumpManager.Application package

# Remove package
dotnet remove src/InfoDumpManager.Application package OldPackage
```

---

## Security Considerations

### Authentication & Authorization

```csharp
// JWT Bearer token authentication
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new()
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ValidateIssuer = true,
            ValidIssuer = "InfoDumpManager",
            ValidateAudience = true,
            ValidAudience = "InfoDumpManager-API"
        };
    });

// Claims-based authorization
[Authorize(Policy = "MustBeOwnerOfGEM")]
[HttpDelete("api/v1/gems/{id}")]
public async Task<IActionResult> DeleteGEM(GEMId id)
```

### Secrets Management

**✅ DO**:
- Store secrets in `.env` (git-ignored) or environment variables
- Use `IConfiguration` to access secrets
- Reference `.env.template` for required variables
- Rotate API keys regularly
- Use least-privilege service accounts

**❌ DON'T**:
- Hardcode secrets in source code
- Commit `.env` file to Git
- Log API keys or passwords
- Share secrets via Slack/email
- Use same secrets across environments

```bash
# Example .env file structure
ASPNETCORE_ENVIRONMENT=Development
LLM_PROVIDER=OpenAI
OPENAI_API_KEY=sk-...
OPENAI_MODEL=gpt-4-turbo
JWT_SECRET_KEY=<long-random-key>
DATABASE_CONNECTION_STRING=Host=localhost;Username=postgres;Password=postgres;Database=infodumpmanager
REDIS_CONNECTION_STRING=localhost:6379
```

### Input Validation & Sanitization

```csharp
// ✅ Use FluentValidation
public class CreateGEMCommandValidator : AbstractValidator<CreateGEMCommand>
{
    public CreateGEMCommandValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty()
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("Invalid URL");
        
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(500)
            .Matches(@"^[a-zA-Z0-9\s\-.,!?()]*$")
            .WithMessage("Title contains invalid characters");
    }
}

// ✅ Use parameterized queries (EF Core does this automatically)
var gem = await dbContext.GEMs
    .Where(x => x.Id == id && x.UserId == userId)  // No SQL injection
    .FirstOrDefaultAsync();

// ❌ Never concatenate SQL
var sql = $"SELECT * FROM gems WHERE id = {id}";  // SQL injection risk!
```

### Data Encryption

```csharp
// ✅ Encrypt sensitive data at rest (optional, if not handled by database)
public class EncryptionService
{
    public string Encrypt(string plaintext) => /* AES encryption */
    public string Decrypt(string ciphertext) => /* AES decryption */
}

// ✅ Configure column encryption in EF Core
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<GEM>()
        .Property(e => e.ApiKey)
        .HasConversion(new EncryptedConverter(encryptionService));
}

// ✅ Use HTTPS/TLS for all traffic
options.UseHttpsRedirection();
options.UseHsts();
```

### Logging & Monitoring Security

```csharp
// ✅ Log security events but NOT secrets
_logger.LogWarning("Failed login attempt for user {UserId} from IP {IpAddress}", userId, ipAddress);

// ❌ Never log sensitive data
_logger.LogInformation("API Key: {ApiKey}");  // DON'T DO THIS

// ✅ Implement audit logging for sensitive operations
public class AuditLogMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Log action, user, timestamp, IP address
        _auditLogger.LogAsync(new AuditLog
        {
            UserId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            Action = $"{context.Request.Method} {context.Request.Path}",
            IpAddress = context.Connection.RemoteIpAddress?.ToString(),
            Timestamp = DateTime.UtcNow
        });
    }
}
```

### Security Testing

```bash
# Run security-focused tests
dotnet test tests/ --filter "Category=Security"

# OWASP dependency check
dotnet add package OwasDependencyCheck

# Static code analysis
dotnet analyze --severity warning

# Review all NuGet packages for vulnerabilities
dotnet list package --vulnerable
```

---

## Pull Request Guidelines

### Title Format

```
[<PHASE>] <COMPONENT> - Brief description of change

Examples:
- [Phase 1] Domain - Implement GEM aggregate root with validation
- [Phase 2] API - Add summarization background service
- [Phase 3] Search - Implement hybrid search with pgvector
- [Phase 4] Observability - Add Prometheus metrics and Grafana dashboard
```

### Branch Naming Convention

```
feature/<phase>/<component>-<description>
bugfix/<component>-<description>
docs/<topic>-<description>

Examples:
- feature/phase1/domain-gem-aggregate
- bugfix/api-null-reference-exception
- docs/setup-development-environment
```

### Commit Message Format

```
<type>(<scope>): <subject>

<body>

<footer>

Types: feat, fix, docs, style, refactor, test, chore, ci
Scope: domain, application, infrastructure, webapi, web, tests

Examples:
feat(domain): add GEM aggregate root with validation
fix(api): resolve null reference in category controller
docs(readme): update development setup instructions
test(integration): add database migration tests
```

### Required Checks Before Submission

```bash
# 1. Rebuild solution
dotnet build

# 2. Run all tests
dotnet test

# 3. Check code coverage (target 80%+)
dotnet test /p:CollectCoverage=true

# 4. Run code analysis
dotnet build /p:EnableNETAnalyzers=true /p:AnalysisModeStyle=All

# 5. Format code (if using EditorConfig)
dotnet format

# 6. Verify no secrets committed
git grep -i "password\|secret\|api.key\|token" -- ':!/docs/' ':!/*.env*'
```

### PR Description Template

```markdown
## Description
Brief description of what this PR accomplishes.

## Related Issue
Closes #<issue-number> or Related to <epic/task>

## Type of Change
- [ ] New feature (Phase 1/2/3/4)
- [ ] Bug fix
- [ ] Documentation update
- [ ] Refactoring
- [ ] Performance improvement

## Changes Made
- Detailed list of changes
- One per line
- Reference implementation plan tasks (TASK-XXX)

## Testing
- [ ] Unit tests added/updated
- [ ] Integration tests added/updated
- [ ] Manual testing performed
- [ ] Code coverage target met (80%+)

## Architecture Impact
- [ ] No breaking changes
- [ ] Breaking changes documented
- [ ] Database migration required (if yes, tested locally)
- [ ] Configuration changes (if yes, update .env.template)

## Security Checklist
- [ ] No secrets hardcoded
- [ ] Input validation implemented
- [ ] Authorization checks in place
- [ ] Sensitive data logged appropriately (not passwords/keys)

## Deployment Notes
- Any special deployment steps?
- Database migrations to run?
- Configuration changes required?
- Backwards compatibility concerns?
```

### Code Review Expectations

**Reviewers will check**:
1. Architecture adherence (DDD, CQRS, layering)
2. Test coverage (unit + integration)
3. Security (input validation, secrets management)
4. Performance implications
5. Documentation completeness
6. Code style consistency
7. Database migration safety
8. Backwards compatibility

**Expected approval**: 1 approval before merge (2 for sensitive areas: security, schema changes)

---

## Debugging and Troubleshooting

### Common Issues

#### Database Connection Errors

```
Error: Unable to connect to PostgreSQL

Solution:
1. Check if containers are running: docker ps
2. Start containers: docker-compose up -d
3. Verify connection string in .env
4. Test connection: dotnet ef dbcontext info
5. Check logs: docker-compose logs db
```

#### EF Core Migration Issues

```
Error: "No database provider has been configured"

Solution:
1. Ensure startup project is set: --startup-project src/InfoDumpManager.WebAPI
2. Add migration: dotnet ef migrations add InitialCreate --startup-project src/InfoDumpManager.WebAPI
3. Update database: dotnet ef database update --startup-project src/InfoDumpManager.WebAPI
```

#### LLM API Failures

```
Error: OpenAI API key invalid or rate limited

Solutions:
1. Verify API key in .env file
2. Check API key permissions in OpenAI dashboard
3. Implement retry logic: Polly circuit breaker
4. Monitor token usage and costs
5. Implement request throttling
```

#### Background Job Processing Hangs

```
Error: Summarization jobs not processing

Solutions:
1. Check Redis connection: docker-compose logs redis
2. Check job queue: inspect Channels in debugger
3. Verify background service is running
4. Check background service logs in Serilog output
5. Manually process job for testing
```

#### Vector Search Performance

```
Error: pgvector similarity search slow

Solutions:
1. Verify pgvector extension is installed: SELECT * FROM pg_extension WHERE extname='vector';
2. Create HNSW index: CREATE INDEX ON embeddings USING hnsw (embedding vector_cosine_ops);
3. Profile query: EXPLAIN ANALYZE SELECT ... ORDER BY embedding <-> query_vector;
4. Check embedding dimension matches (should be 1536 for OpenAI)
5. Analyze index statistics: ANALYZE embeddings;
```

### Debugging Techniques

#### Local Debugging with Breakpoints

```bash
# Run in debug mode (enables breakpoints in IDE)
dotnet run --project src/InfoDumpManager.WebAPI

# In Visual Studio: F5 to start debugging
# In VS Code: Install C# extension, create .vscode/launch.json
# In Rider: Right-click project → Run with Debugging
```

#### Remote Debugging

```bash
# Run application with debugger listening
dotnet run --project src/InfoDumpManager.WebAPI -- --debug

# Attach debugger from IDE (Visual Studio: Debug → Attach to Process)
```

#### Logging Inspection

```bash
# View real-time logs
docker-compose logs -f webapi
docker-compose logs -f web
docker-compose logs -f db

# Search logs for errors
docker-compose logs webapi | grep -i "error\|exception"

# Serilog structured logs (in application output)
# Look for fields: RequestId, UserId, ElapsedMilliseconds, Exception
```

#### Performance Profiling

```bash
# Collect performance trace
dotnet trace collect --process-id <PID> --output trace.nettrace

# View trace in PerfView or Visual Studio
# Look for: hot paths, allocation rates, GC pressure

# Profile specific test
dotnet test --collect:"XPlat Code Coverage" tests/InfoDumpManager.Tests.Unit
```

### Health Check Endpoints

```bash
# Liveness check (is service running?)
curl http://localhost:5000/health/live

# Readiness check (is service ready for requests?)
curl http://localhost:5000/health/ready

# Detailed health report
curl http://localhost:5000/health/detailed
```

### Environment-Specific Debugging

```csharp
// ✅ Conditional logging based on environment
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ✅ Debug-only endpoints
[ApiController]
[Route("api/[controller]")]
#if DEBUG
[AllowAnonymous]
#else
[Authorize]
#endif
public class DebugController : ControllerBase
{
    [HttpGet("seed-data")]
    public async Task SeedDevelopmentData()
    {
        // Development-only seeding
    }
}
```

---

## Additional Notes

### Implementation Phase Progress

Track progress against `docs/ways-of-work/plan/gem-ingestion-categorization/implementation-plan-1.md`:

- **Phase 1 (4-5 weeks)**: Foundation & Basic Ingestion (TASK-001 to TASK-030)
- **Phase 2 (4-5 weeks)**: AI Summarization & Auto-Categorization (TASK-031 to TASK-060)
- **Phase 3 (3-4 weeks)**: Tagging, Search & Q&A Synthesis (TASK-061 to TASK-095)
- **Phase 4 (2-3 weeks)**: Polish, Observability & Production Readiness (TASK-096 to TASK-128)

### Performance Targets

- **NFR-001**: Ingestion + summarization < 15 seconds (p95)
- **NFR-006**: Support tens of GEMs per user per day
- **Database**: Optimize queries with proper indexes
- **Vector Search**: Profile pgvector performance regularly

### Documentation Resources

- Implementation Plan: `docs/ways-of-work/plan/gem-ingestion-categorization/implementation-plan-1.md`
- Architecture Spec: `docs/ways-of-work/plan/gem-ingestion-categorization/arch.md`
- Epic PRD: `docs/ways-of-work/plan/gem-ingestion-categorization/epic.md`
- API Docs: Generated via Swagger at http://localhost:5000/swagger/index.html
- Setup Guide: README.md

### Communication

- **Questions about requirements**: Refer to `implementation-plan-1.md` Section 1 (Requirements & Constraints)
- **Architecture decisions**: Check Section 3 (Alternatives) and Section 7 (Risks & Assumptions)
- **Blocking issues**: Escalate with context and impact assessment
- **Code review**: Create PR with detailed description and checklist completion

### Gotchas & Tips

1. **Database migrations**: Always test migrations in staging environment before production
2. **LLM costs**: Monitor API usage and implement throttling for high-volume scenarios
3. **Background jobs**: Use Polly for resilience; single-instance jobs may need scaling
4. **Vector embeddings**: Dimension must match provider (OpenAI = 1536)
5. **Secrets**: Never commit `.env` file; use `.env.template` instead
6. **Docker Compose**: Depends on service name resolution (e.g., `Host=db:5432`)
7. **EF Core migrations**: Include startup project: `--startup-project src/InfoDumpManager.WebAPI`

---

**Last Updated**: 2026-01-23  
**Created for**: Multi-agent implementation of InfoDumpManager GEM System  
**Reference**: Implementation Plan v1.0
