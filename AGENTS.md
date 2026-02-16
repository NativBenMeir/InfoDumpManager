# AGENTS.md

## Project Overview

InfoDumpManager is a multi-layered .NET 8 application for GEM (General Event Management) ingestion, summarization, and smart categorization. It follows a clean architecture pattern with domain-driven design principles.

**Key Technologies:**
- **.NET 8** - Target framework
- **Entity Framework Core 8** - ORM for data persistence
- **PostgreSQL 16** - Primary database with pgvector support
- **Redis 7** - Caching and session management
- **MinIO** - Object storage
- **Serilog** - Structured logging
- **xUnit** - Testing framework
- **Docker & Docker Compose** - Containerization and orchestration
- **Nginx** - Reverse proxy (configured in docker-compose)

**Project Structure:**
- `InfoDumpManager.Domain` - Domain entities, value objects, and business logic
- `InfoDumpManager.Application` - Application services and use cases
- `InfoDumpManager.Infrastructure` - Data access, EF Core DbContext, and migrations
- `InfoDumpManager.WebAPI` - RESTful API with Swagger/OpenAPI
- `InfoDumpManager.Web` - Web UI (Razor Pages)
- `tests/InfoDumpManager.Tests.Unit` - Unit tests
- `tests/InfoDumpManager.Tests.Integration` - Integration tests using Testcontainers

## Setup Commands

### Prerequisites
- .NET 8 SDK installed
- Docker & Docker Compose (for running databases)
- Visual Studio Code or Visual Studio 2022 (recommended)

### Initial Setup

```bash
# Restore all NuGet packages
dotnet restore

# Apply database migrations (requires running PostgreSQL via docker-compose)
dotnet ef database update --project src/InfoDumpManager.Infrastructure --startup-project src/InfoDumpManager.WebAPI
```

### Start Development Environment

```bash
# Start dependent services (PostgreSQL, Redis, MinIO)
docker-compose up -d

# Build the solution
dotnet build

# Run the WebAPI
dotnet run --project src/InfoDumpManager.WebAPI

# Run the Web UI
dotnet run --project src/InfoDumpManager.Web
```

## Development Workflow

### Running Services

The project uses Docker Compose to manage dependent services:

```bash
# Start all services in background
docker-compose up -d

# Stop all services
docker-compose down

# View logs for a specific service
docker-compose logs postgres
docker-compose logs redis
docker-compose logs minio

# Restart services
docker-compose restart
```

**Services and Ports:**
- PostgreSQL: `localhost:5432` (User: `infodump`, Password: `dev_password_change_in_production`, DB: `infodumpmanager`)
- Redis: `localhost:6379`
- MinIO API: `localhost:9000` (User: `minioadmin`, Password: `minioadmin123`)
- MinIO Console: `localhost:9001`

### Building the Solution

```bash
# Build entire solution
dotnet build

# Build specific project
dotnet build src/InfoDumpManager.WebAPI

# Release build
dotnet build -c Release
```

### Running Applications

**WebAPI Development:**
```bash
dotnet run --project src/InfoDumpManager.WebAPI
# Access: http://localhost:5000
# Swagger UI: http://localhost:5000/swagger
```

**Web UI Development:**
```bash
dotnet run --project src/InfoDumpManager.Web
# Access: http://localhost:5001 (or as configured in launchSettings.json)
```

### Database Migrations

```bash
# Create a new migration
dotnet ef migrations add <MigrationName> --project src/InfoDumpManager.Infrastructure --startup-project src/InfoDumpManager.WebAPI

# Apply pending migrations
dotnet ef database update --project src/InfoDumpManager.Infrastructure --startup-project src/InfoDumpManager.WebAPI

# Remove last migration
dotnet ef migrations remove --project src/InfoDumpManager.Infrastructure --startup-project src/InfoDumpManager.WebAPI

# Generate migration script (useful for production deployment)
dotnet ef migrations script --project src/InfoDumpManager.Infrastructure --startup-project src/InfoDumpManager.WebAPI -o scripts/migration.sql
```

## Testing Instructions

### Running Tests

The project uses **xUnit** for both unit and integration tests. Integration tests use **Testcontainers** to spin up PostgreSQL in Docker automatically.

```bash
# Run all tests
dotnet test

# Run only unit tests
dotnet test tests/InfoDumpManager.Tests.Unit

# Run only integration tests
dotnet test tests/InfoDumpManager.Tests.Integration

# Run with verbose output
dotnet test -v n

# Run tests with code coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

### Test File Locations

- **Unit Tests:** `tests/InfoDumpManager.Tests.Unit/`
- **Integration Tests:** `tests/InfoDumpManager.Tests.Integration/`
- **Test Fixtures:** `tests/InfoDumpManager.Tests.Integration/Fixtures/`

### Test Patterns

- **Naming Convention:** `[FeatureName][Scenario][ExpectedOutcome]` (e.g., `DbContextCanConnect`)
- **xUnit Attributes:** Use `[Fact]` for parameterless tests, `[Theory]` with `[InlineData]` for parameterized tests
- **Fixtures:** Use xUnit collection fixtures for setup/teardown of test containers

### Coverage Requirements

- Target minimum 80% code coverage for domain and application layers
- Integration test coverage for database operations and external dependencies
- Run coverage reports with coverlet: `dotnet test /p:CollectCoverage=true`

### Running tests

 - There are hundreds of test in the solution. If no tests run it means the build has failed. Make sure to check and fix build errors and then rerun tests.

## Code Style Guidelines

### C# Conventions

- **Framework:** Target .NET 8.0 with nullable reference types enabled (`<Nullable>enable</Nullable>`)
- **Implicit Usings:** Enabled globally - no need for common `using` statements
- **Naming:** PascalCase for classes, methods, properties; camelCase for local variables and parameters
- **Access Modifiers:** Explicitly specify access levels (public, private, internal, etc.)
- **Async:** Use async/await for I/O operations; method names should end with `Async`

### Project Organization

```
src/
├─ <ProjectName>/
   ├─ <FeatureFolder>/
   │  ├─ Entities/          (Domain entities)
   │  ├─ ValueObjects/      (Value objects)
   │  ├─ Services/          (Application services)
   │  ├─ Repositories/      (Data access abstractions)
   │  └─ DTOs/             (Data transfer objects)
   ├─ Common/               (Shared across features)
   └─ Extensions/           (Extension methods)
```

### Clean Architecture Principles

- **Domain Layer:** No external dependencies, contains business logic
- **Application Layer:** Uses domain but not infrastructure; orchestrates business operations
- **Infrastructure Layer:** Implements repository patterns, EF Core configurations, external services
- **Presentation Layer:** Web or API endpoints; orchestrates with application services

### Configuration

- **Configuration Files:** Use `appsettings.json` and `appsettings.{Environment}.json`
- **Secrets Management:** Development secrets in `secrets.json`, production via environment variables
- **Logging:** Configured via Serilog in `Program.cs`; use structured logging with properties

## Build and Deployment

### Building

```bash
# Development build
dotnet build

# Release build with optimizations
dotnet build -c Release

# Publish for deployment
dotnet publish -c Release -o ./publish
```

### Publishing Output

Release builds produce assemblies in the respective project `bin/Release/net8.0/` directories. For production deployment, use:

```bash
dotnet publish -c Release --self-contained -r win-x64 (or linux-x64 for Linux)
```

### Docker Deployment

```bash
# Build Docker images (if Dockerfiles exist in projects)
docker build -f src/InfoDumpManager.WebAPI/Dockerfile -t infodump-api:latest .

# Run with docker-compose
docker-compose up

# Build and start fresh
docker-compose up --build
```

### Environment Configuration

- **Development:** Serilog logs to console and file (`logs/infodumpmanager-*.log`)
- **Production:** Set `ASPNETCORE_ENVIRONMENT=Production` and configure real database credentials via environment variables
- **Database:** PostgreSQL connection strings configured in `appsettings.json`; override with `ConnectionStrings:DefaultConnection` environment variable

### Deployment Checklist

- [ ] Update connection strings for production database
- [ ] Generate and apply all pending migrations: `dotnet ef database update --project src/InfoDumpManager.Infrastructure`
- [ ] Configure logging levels appropriately (reduce verbosity in production)
- [ ] Set secure secrets for authentication and external services
- [ ] Verify all health checks pass before going live
- [ ] Run integration tests against staging environment
- [ ] Enable HTTPS and configure SSL certificates

## Pull Request Guidelines

### Title Format

Use the following format for PR titles to maintain consistency and enable automated tooling:

```
[<Component>] <Brief description>
```

**Components:** `Domain`, `Application`, `Infrastructure`, `WebAPI`, `Web`, `Tests`, `Docs`

**Examples:**
- `[Domain] Add GEM aggregate root with snapshot support`
- `[Infrastructure] Implement repository pattern for data access`
- `[WebAPI] Add Swagger documentation for health endpoint`
- `[Tests] Add integration tests for database operations`

### Pre-Submission Checklist

Before submitting a PR, ensure:

1. **Code Builds:** `dotnet build` completes without errors
2. **Tests Pass:** `dotnet test` all tests pass
3. **No Warnings:** Address any compiler warnings
4. **Code Style:** Follow C# conventions and project organization patterns
5. **Documentation:** Add XML comments for public API methods
6. **Migrations:** If database changes, create and test migrations

### Review Requirements

- Minimum 1 approval required before merge
- All tests must pass in CI/CD pipeline
- No merge conflicts
- Code follows established patterns in the codebase

### Commit Message Conventions

Use conventional commit format:

```
<type>(<scope>): <subject>

<body>
```

**Types:** `feat`, `fix`, `docs`, `refactor`, `test`, `chore`

**Example:**
```
feat(domain): add GEM snapshot value object

Implement GEMSnapshot to capture point-in-time GEM state
```

## Security Considerations

### Authentication & Authorization

- Phase 1: No authentication (development only)
- Phase 2+: Implement JWT-based authentication with claims
- Multi-tenancy support via ITenantEntity interface (implemented in domain entities)

### Data Protection

- Use Entity Framework Core's data protection API for sensitive fields
- Store connection strings and secrets in secure configuration (environment variables, Azure Key Vault, etc.)
- Never commit secrets to version control
- PostgreSQL pgvector extension for vector operations (ML/AI embeddings)

### Database Security

- Always use parameterized queries (EF Core handles this)
- Implement soft delete where appropriate (GEM has `IsDeleted` flag simulation)
- Enforce foreign key constraints (all configured in EF Core)
- Use PostgreSQL row-level security for multi-tenant data isolation

## Monorepo Instructions

This is a multi-project solution with layered architecture, not a monorepo. However, work with projects as follows:

```bash
# Build a specific layer
dotnet build src/InfoDumpManager.Infrastructure

# Run tests for a specific layer
dotnet test tests/InfoDumpManager.Tests.Unit

# Restore dependencies for entire solution
dotnet restore InfoDumpManager.sln
```

## Debugging and Troubleshooting

### Common Issues

**PostgreSQL Connection Failures**
```bash
# Verify PostgreSQL is running
docker-compose ps postgres

# Check logs
docker-compose logs postgres

# Restart PostgreSQL
docker-compose restart postgres
```

**Migration Issues**
```bash
# Check current migrations
dotnet ef migrations list --project src/InfoDumpManager.Infrastructure

# Revert to previous migration
dotnet ef migrations remove --project src/InfoDumpManager.Infrastructure

# Manually reset database (CAUTION: Removes all data)
docker-compose exec postgres psql -U infodump -d infodumpmanager -c "DROP SCHEMA public CASCADE; CREATE SCHEMA public;"
```

**Test Failures**
```bash
# Run with verbose output
dotnet test -v n

# Run specific test class
dotnet test --filter "EFCoreIntegrationTests"

# Run with specific logger
dotnet test -l "console;verbosity=detailed"
```

### Logging Configuration

- **Log Location:** `logs/` directory in working directory
- **Log Level:** Configure in `appsettings.json` under `Serilog:MinimumLevel`
- **Development:** Console and file output enabled
- **Production:** Adjust sink destinations and verbosity levels

### Performance Considerations

- **Database Indexes:** Created on commonly queried columns (configured in EF Core)
- **Query Optimization:** Use `.AsNoTracking()` for read-only queries
- **Batch Operations:** Use `BulkInsert` patterns for large data operations
- **Caching:** Redis configured for session and distributed caching

### Debug Configuration

In Visual Studio or VS Code, set breakpoints in code and use the debugger. For command-line debugging:

```bash
# Run with debugger attached
dotnet run --project src/InfoDumpManager.WebAPI

# Debug tests
dotnet test tests/InfoDumpManager.Tests.Unit -- --debug
```

## Additional Notes

### Project Status

- **Phase 1-2:** Database schema design, EF Core configuration, migrations ✅ COMPLETE
- **Phase 3+:** API endpoint implementation, business logic, UI development (pending)

### Configuration Files

- `appsettings.json` - Base configuration
- `appsettings.Development.json` - Development overrides
- `.vscode/settings.json` - VS Code workspace settings
- `docker-compose.yml` - Local development services

### Documentation

- [API Documentation](docs/api.md) - Endpoint specifications
- [Database Schema](scripts/init-db.sql) - SQL initialization script
- [Architecture Design](docs/) - Design decisions and patterns

### Contact & Support

- Review the [COMPLETION-REPORT.md](COMPLETION-REPORT.md) for detailed implementation status
- Check GitHub Issues for known problems and feature requests
- Consult the .DesignDocs folder for architecture and design documentation

