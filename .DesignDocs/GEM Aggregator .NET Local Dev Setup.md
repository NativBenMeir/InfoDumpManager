# GEM Aggregator — .NET (Local Dev Setup)

## Local-First Architecture
- Deployables: `Gem.Api` (ASP.NET Core 8), `Gem.Worker` (Hosted Service/Hangfire), `Gem.Web` (Blazor Server/WASM).
- Shared lib: `Gem.Shared` for models/DTOs/interfaces.
- Data/queues/cache all run via Docker Compose on your machine.

## Local Infra (Docker Compose)
- **PostgreSQL + pgvector**: primary DB.
- **Redis**: cache + Hangfire storage.
- **MinIO**: S3-compatible blob storage for HTML snapshots.
- **Azurite (optional)**: if you prefer Azure Storage APIs locally.
- **(Optional) Maildev/Mailhog**: to capture inbound/outbound email during tests.

## AI (Local Dev)
- Use **OpenAI API** (or Azure OpenAI if you have keys). Configure via env vars:
  - `OPENAI_API_KEY` (or `AZURE_OPENAI_ENDPOINT` / `AZURE_OPENAI_KEY`)
  - Model: `gpt-4o` (or your Azure deployment name)
  - Embeddings: `text-embedding-3-large`
- You can stub AI in tests with an in-memory `IAiService` mock.

## Queues & Jobs
- **Hangfire + Redis** for background jobs (ingest/summarize/categorize/feedback).
- No Service Bus needed locally.

## Configuration (appsettings.Development.json)
- ConnectionStrings for Postgres/Redis.
- MinIO endpoint/access/secret.
- OpenAI/Azure OpenAI keys.
- CORS: allow `https://localhost:5001` and `https://localhost:5173` (adjust to your ports).

## Running Locally
1) `docker compose up -d` (starts Postgres, Redis, MinIO, optional Maildev/Azurite).
2) `dotnet restore`
3) `dotnet ef database update` (from `Gem.Api` project) to create schema with pgvector.
4) Run apps:
   - API: `dotnet run --project src/Gem.Api`
   - Worker: `dotnet run --project src/Gem.Worker`
   - Web: `dotnet run --project src/Gem.Web`
5) Access:
   - API swagger: `https://localhost:5001/swagger`
   - Web UI: `https://localhost:5173` (or the port your Blazor app uses)
   - MinIO console: `http://localhost:9001`

## Local Development Defaults (env)
- `ASPNETCORE_ENVIRONMENT=Development`
- `POSTGRES_CONNECTION=Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=gemdb`
- `REDIS_CONNECTION=localhost:6379`
- `MINIO_ENDPOINT=http://localhost:9000`
- `MINIO_ACCESS_KEY=minioadmin`
- `MINIO_SECRET_KEY=minioadmin`
- `OPENAI_API_KEY=...` (or Azure OpenAI vars)
- `HANGFIRE_DASHBOARD_AUTH_DISABLED=true` (dev only)

## Docker Compose (snippet)
```yaml
services:
  postgres:
    image: pgvector/pgvector:pg16
    environment:
      POSTGRES_PASSWORD: postgres
      POSTGRES_DB: gemdb
    ports: ["5432:5432"]
  redis:
    image: redis:7
    ports: ["6379:6379"]
  minio:
    image: minio/minio
    command: server /data
    environment:
      MINIO_ACCESS_KEY: minioadmin
      MINIO_SECRET_KEY: minioadmin
    ports: ["9000:9000", "9001:9001"]
```

## Minimal EF Model (example)
```csharp
// filepath: src/Gem.Api/Data/GemDbContext.cs
// ...existing code...
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.HasPostgresExtension("vector");
    modelBuilder.Entity<Gem>()
        .Property(e => e.Embedding)
        .HasColumnType("vector(1536)");
}
// ...existing code...
```

## Auth (Dev)
- Start with no auth or simple API key header for local.
- Add Entra ID/OIDC when moving to Azure.

## Tests
- Unit tests: mock IAiService, IVectorStoreService, IConnectorService.
- Integration tests: use Testcontainers for Postgres/Redis/MinIO; seed data and run ingest→summarize→categorize→QA flow.

## Migration to Azure (later)
- Swap MinIO→Blob Storage, Redis→Azure Cache for Redis, Hangfire storage→SQL/Redis managed, optionally move queues to Service Bus, keep Postgres managed (Flexible Server).
```