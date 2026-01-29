# InfoDumpManager - Phase 1 Documentation

## Overview

This document provides setup and usage instructions for Phase 1 of the GEM Ingestion, Summarization, and Smart Categorization System.

## Project Structure

```
InfoDumpManager/
├── src/
│   ├── InfoDumpManager.Domain/          # Domain layer (entities, value objects, aggregates)
│   ├── InfoDumpManager.Application/     # Application layer (use cases, DTOs, interfaces)
│   ├── InfoDumpManager.Infrastructure/  # Infrastructure layer (data access, external services)
│   ├── InfoDumpManager.WebAPI/          # ASP.NET Core Web API
│   └── InfoDumpManager.Web/             # ASP.NET Core Razor Pages web application
├── tests/
│   ├── InfoDumpManager.Tests.Unit/      # Unit tests
│   └── InfoDumpManager.Tests.Integration/ # Integration tests
├── scripts/
│   └── init-db.sql                      # Database initialization script
├── nginx/
│   └── nginx.conf                       # Nginx reverse proxy configuration
├── docker-compose.yml                   # Docker Compose configuration
└── InfoDumpManager.sln                  # Solution file
```

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (Note: .NET 10.0 specified in plan is not yet available, using .NET 8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- Minimum 8GB RAM

## Getting Started

### 1. Clone the Repository

```bash
git clone <repository-url>
cd InfoDumpManager
```

### 2. Start Docker Services

The project uses Docker Compose to run the following services:

- **PostgreSQL 16** with pgvector extension (port 5432)
- **Redis 7** for caching (port 6379)
- **MinIO** for object storage (ports 9000, 9001)
- **Nginx** as reverse proxy (port 8080)

Start all services:

```bash
docker compose up -d
```

Verify services are running:

```bash
docker compose ps
```

All services should show status as "Up" and healthy.

### 3. Build the Solution

```bash
dotnet build
```

### 4. Run Tests

Run unit tests:

```bash
dotnet test tests/InfoDumpManager.Tests.Unit
```

Run integration tests:

```bash
dotnet test tests/InfoDumpManager.Tests.Integration
```

Run all tests:

```bash
dotnet test
```

### 5. Run the Application

Run the Web API:

```bash
cd src/InfoDumpManager.WebAPI
dotnet run
```

The API will be available at:
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`
- Swagger UI: `http://localhost:5000/swagger`

Run the Web Application:

```bash
cd src/InfoDumpManager.Web
dotnet run
```

## Docker Services

### PostgreSQL

- **Host:** localhost
- **Port:** 5432
- **Database:** infodumpmanager
- **Username:** infodump
- **Password:** dev_password_change_in_production

Connect using psql:

```bash
docker exec -it infodump-postgres psql -U infodump -d infodumpmanager
```

View seeded categories:

```sql
SELECT * FROM categories;
```

View seeded tags:

```sql
SELECT * FROM tags;
```

### Redis

- **Host:** localhost
- **Port:** 6379

Test connection:

```bash
docker exec -it infodump-redis redis-cli ping
```

### MinIO

- **API Endpoint:** http://localhost:9000
- **Console:** http://localhost:9001
- **Username:** minioadmin
- **Password:** minioadmin123

Access the MinIO console at http://localhost:9001

### Nginx

- **Port:** 8080
- **Routes:**
  - `/api/*` → Web API (localhost:5000)
  - `/*` → Web Application (localhost:5001)

## API Documentation

### Swagger/OpenAPI

The API documentation is automatically generated and available at:

- **Swagger UI:** http://localhost:5000/swagger
- **OpenAPI Spec:** http://localhost:5000/swagger/v1/swagger.json

### Sample Endpoint

The template includes a sample Weather Forecast endpoint:

**GET** `/weatherforecast`

Response:
```json
[
  {
    "date": "2026-01-29",
    "temperatureC": 15,
    "temperatureF": 59,
    "summary": "Mild"
  }
]
```

## Logging

The application uses Serilog for structured logging with the following configuration:

### Console Sink

Logs are written to the console with the format:
```
[HH:mm:ss LEVEL] Message {Properties}
```

### File Sink

Logs are written to `logs/infodumpmanager-{Date}.log` with:
- Daily rolling interval
- 7-day retention policy
- Detailed timestamp format

### Log Levels

- **Development:** Debug
- **Production:** Information

Override log levels in `appsettings.Development.json` or `appsettings.json`.

View logs:

```bash
# Real-time console logs
dotnet run

# File logs
cat logs/infodumpmanager-$(date +%Y%m%d).log
```

## Configuration

### Connection Strings

Database connection strings are configured in:
- `src/InfoDumpManager.WebAPI/appsettings.json`
- `src/InfoDumpManager.WebAPI/appsettings.Development.json`

Default connection string:
```
Host=localhost;Port=5432;Database=infodumpmanager;Username=infodump;Password=dev_password_change_in_production
```

### Environment Variables

Override configuration using environment variables:

```bash
export ASPNETCORE_ENVIRONMENT=Development
export ConnectionStrings__DefaultConnection="Host=localhost;..."
```

## Development Workflow

### Adding a New Project

1. Create the project in the appropriate folder (`src/` or `tests/`)
2. Add project reference to solution:
   ```bash
   dotnet sln add path/to/Project.csproj
   ```

### Adding Dependencies

Add NuGet packages:
```bash
dotnet add package PackageName --version X.Y.Z
```

### Database Migrations

Database schema is managed via SQL scripts in `scripts/init-db.sql`.

To apply changes:
1. Update `scripts/init-db.sql`
2. Recreate the database:
   ```bash
   docker compose down -v
   docker compose up -d
   ```

## Troubleshooting

### Docker Services Not Starting

Check Docker Desktop is running:
```bash
docker ps
```

View service logs:
```bash
docker compose logs <service-name>
docker compose logs postgres
```

Restart services:
```bash
docker compose restart
```

### Build Errors

Clean and rebuild:
```bash
dotnet clean
dotnet build
```

### Database Connection Issues

Verify PostgreSQL is running:
```bash
docker compose ps postgres
```

Test connection:
```bash
docker exec infodump-postgres pg_isready -U infodump
```

### Port Conflicts

If ports are already in use, modify `docker-compose.yml`:

```yaml
services:
  postgres:
    ports:
      - "15432:5432"  # Use different host port
```

## Architecture

### Domain-Driven Design Layers

1. **Domain Layer** (`InfoDumpManager.Domain`)
   - Pure business logic
   - No external dependencies
   - Contains: Entities, Value Objects, Domain Events, Aggregates

2. **Application Layer** (`InfoDumpManager.Application`)
   - Use cases and application services
   - Uses MediatR for CQRS pattern
   - Contains: Commands, Queries, DTOs, Validators (FluentValidation)

3. **Infrastructure Layer** (`InfoDumpManager.Infrastructure`)
   - External concerns (database, file system, APIs)
   - Implements repository pattern with Unit of Work
   - Contains: DbContext, Repositories, External Service Clients

4. **Presentation Layer** (`InfoDumpManager.WebAPI`, `InfoDumpManager.Web`)
   - HTTP endpoints and UI
   - Thin layer that delegates to Application layer

### Key Patterns

- **CQRS** - Separate read and write models using MediatR
- **Repository Pattern** - Abstract data access
- **Unit of Work** - Manage transactions
- **Dependency Injection** - All services use constructor injection

## Next Steps

Phase 1 establishes the foundation. Future phases will implement:
- Domain entities (GEM, Category, Tag)
- Web page ingestion
- LLM-based summarization
- Smart categorization
- Full-text search
- User interface

## Additional Resources

- [ASP.NET Core Documentation](https://docs.microsoft.com/en-us/aspnet/core/)
- [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/)
- [Serilog](https://serilog.net/)
- [MediatR](https://github.com/jbogard/MediatR)
- [FluentValidation](https://fluentvalidation.net/)
- [Docker Compose](https://docs.docker.com/compose/)
- [pgvector](https://github.com/pgvector/pgvector)
