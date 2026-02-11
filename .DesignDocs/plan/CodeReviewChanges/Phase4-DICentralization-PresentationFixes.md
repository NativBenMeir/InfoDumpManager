# Phase 4: Centralize DI Registration & Presentation Layer Fixes

status: 'Completed'

![Status: Completed](https://img.shields.io/badge/status-Completed-brightgreen)

**Goal:** Extract duplicated service registration into shared extension methods, remove `Console.WriteLine` calls, and fix `CurrentUserContext` error handling.

**Prerequisites:** Phase 3 complete and building.

**Validation:** `dotnet build` and `dotnet test` from solution root.

---

## 4.1 — Create Shared DI Extension Methods

### Problem
`src/InfoDumpManager.WebAPI/Program.cs` and `src/InfoDumpManager.Web/Program.cs` both contain ~80 lines of identical service registrations (EF Core, repositories, agents, Redis, Polly, MediatR, AutoMapper, etc.). Changes must be made in two places.

### New File: `src/InfoDumpManager.Application/DependencyInjection.cs`

Registers Application-layer services (MediatR, AutoMapper, FluentValidation, pipeline behaviors).

```csharp
using System.Reflection;
using FluentValidation;
using InfoDumpManager.Application.Common.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace InfoDumpManager.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Application-layer services: MediatR, AutoMapper, FluentValidation, pipeline behaviors.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(AssemblyReference).Assembly;

        services.AddAutoMapper(assembly);

        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(assembly);
        });

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
```

### New File: `src/InfoDumpManager.Infrastructure/DependencyInjection.cs`

Registers Infrastructure-layer services (EF Core, repositories, Redis, agents, LLM, embeddings, storage, Polly, background services).

```csharp
using InfoDumpManager.Application.Agents;
using InfoDumpManager.Application.Agents.Implementations;
using InfoDumpManager.Application.Agents.Orchestration;
using InfoDumpManager.Application.Common.Services;
using InfoDumpManager.Application.Infrastructure.JobQueue;
using InfoDumpManager.Application.Services.Caching;
using InfoDumpManager.Application.Services.CostManagement;
using InfoDumpManager.Application.Services.Embeddings;
using InfoDumpManager.Application.Services.LLM;
using InfoDumpManager.Application.Services.Storage;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Infrastructure.Data;
using InfoDumpManager.Infrastructure.Repositories;
using InfoDumpManager.Infrastructure.Services;
using InfoDumpManager.Infrastructure.Services.Caching;
using InfoDumpManager.Infrastructure.Services.Embeddings;
using InfoDumpManager.Infrastructure.Services.LLM;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Npgsql;
using Polly;
using StackExchange.Redis;

namespace InfoDumpManager.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Infrastructure-layer services: EF Core, repositories, Redis, agents,
    /// LLM, embeddings, storage, Polly policies, and background services.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // --- Database ---
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? Environment.GetEnvironmentVariable("CONNECTION_STR");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("The default connection string is not configured.");
        }

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector();
        var dataSource = dataSourceBuilder.Build();

        services.AddScoped<DomainEventDispatchInterceptor>();

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
            options.UseNpgsql(dataSource, sql =>
            {
                sql.EnableRetryOnFailure();
                sql.UseVector();
            })
            .AddInterceptors(sp.GetRequiredService<DomainEventDispatchInterceptor>()));

        // --- Repositories ---
        services.AddScoped<IGEMRepository, GEMRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<ICategorySuggestionRepository, CategorySuggestionRepository>();
        services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
        services.AddScoped<ICostUsageRepository, CostUsageRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // --- Cost Management ---
        services.AddScoped<ICostManager, CostManagerImpl>();
        services.Configure<CostManagementOptions>(configuration.GetSection("CostManagement"));

        // --- LLM & Embeddings ---
        services.Configure<LLMRateLimitOptions>(configuration.GetSection("LLMRateLimit"));
        services.AddSingleton<Kernel>(_ => Kernel.CreateBuilder().Build());
        services.AddSingleton<ILLMProvider, SemanticKernelProvider>();
        services.AddSingleton<ILLMRateLimiter, TenantRateLimiter>();
        services.AddScoped<IEmbeddingProvider, DeterministicEmbeddingProvider>();
        services.AddScoped<IVectorStore, PostgreSqlVectorStore>();

        // --- Redis ---
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisConfiguration = configuration.GetConnectionString("Redis")
                ?? configuration["Redis:Configuration"]
                ?? "localhost:6379";
            return ConnectionMultiplexer.Connect(redisConfiguration);
        });
        services.AddSingleton<IEmbeddingCache, RedisEmbeddingCache>();
        services.AddSingleton<ITextCache, RedisTextCache>();

        // --- Storage & Scraping ---
        services.Configure<MinioOptions>(configuration.GetSection("Minio"));
        services.AddScoped<IStorageService, MinioStorageService>();
        services.Configure<WebScrapingOptions>(configuration.GetSection("WebScraping"));
        services.AddScoped<IWebScrapingService, WebScrapingService>();

        // --- Agents & Processing Pipeline ---
        services.AddScoped<IAgent, SummarizationAgent>();
        services.AddScoped<IAgent, CategorizationAgent>();
        services.AddScoped<IAgent, TaggingAgent>();
        services.AddScoped<IAgent, ValidationAgent>();

        services.AddSingleton<IJobTracker, InMemoryJobTracker>();
        services.AddSingleton<IJobQueue<ProcessingJob>, InMemoryJobQueue<ProcessingJob>>();
        services.AddSingleton<IContentProcessingOrchestrator, ContentProcessingOrchestrator>();
        services.AddHostedService<ContentProcessingBackgroundService>();

        // --- Resilience ---
        var retryPolicy = Policy.Handle<Exception>()
            .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));
        var breakerPolicy = Policy.Handle<Exception>()
            .CircuitBreakerAsync(2, TimeSpan.FromSeconds(30));
        var databasePolicy = Policy.WrapAsync(retryPolicy, breakerPolicy);

        services.AddSingleton<IAsyncPolicy>(databasePolicy);
        services.AddSingleton<IDatabasePolicy>(sp =>
            new PollyDatabasePolicy(sp.GetRequiredService<IAsyncPolicy>()));

        return services;
    }
}
```

### Note on Missing Types
The extension references several `Options` types (`MinioOptions`, `WebScrapingOptions`, `CostManagementOptions`, `LLMRateLimitOptions`). These currently live in various locations. Verify their namespaces and add appropriate `using` statements when implementing.

---

## 4.2 — Simplify WebAPI Program.cs

### Updated File: `src/InfoDumpManager.WebAPI/Program.cs`

Replace the massive `ConfigureServices` method. The new version calls the shared extension methods and only adds WebAPI-specific concerns (Swagger, JWT, Identity, WebAPI validators).

```csharp
private static void ConfigureServices(IConfiguration configuration, IServiceCollection services)
{
    var jwtSettings = BuildJwtSettings(configuration);
    services.AddSingleton(jwtSettings);

    services.AddControllers()
        .AddJsonOptions(opts => opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    services.AddEndpointsApiExplorer();
    services.AddSwaggerGen(options =>
    {
        // ... (keep existing Swagger configuration unchanged)
    });

    // Shared registrations from Application and Infrastructure layers
    services.AddApplication();
    services.AddInfrastructure(configuration);

    // --- Identity (WebAPI-specific) ---
    services.AddIdentity<User, IdentityRole<Guid>>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = false;
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

    services.ConfigureApplicationCookie(options => { /* ... keep existing ... */ });

    // --- JWT Auth (WebAPI-specific) ---
    var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret));
    services.AddAuthentication(options => { /* ... keep existing ... */ })
        .AddJwtBearer(options => { /* ... keep existing ... */ });

    services.PostConfigure<AuthenticationOptions>(options => { /* ... keep existing ... */ });
    services.AddAuthorization(options =>
    {
        options.AddPolicy("MultiTenant", policy => policy.RequireClaim("tenant_id"));
    });

    // --- WebAPI-specific services ---
    services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
    services.AddScoped<ICurrentUserContext, CurrentUserContext>();
    services.AddScoped<ITokenService, JwtTokenService>();

    // WebAPI-specific validators (auth only — command validators via AddApplication)
    services.AddFluentValidationAutoValidation();
    services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
}
```

### Remove `Console.WriteLine` from Pipeline

**File: `src/InfoDumpManager.WebAPI/Program.cs`**

In `ConfigureServices`:
```csharp
// DELETE this line:
Console.WriteLine("ConfigureServices invoked");
```

In `ConfigurePipeline`:
```csharp
// DELETE this line:
Console.WriteLine($"Configuring pipeline for {env.EnvironmentName}");

// DELETE this entire middleware block:
app.Use(async (context, next) =>
{
    Console.WriteLine($"Incoming request: {context.Request.Method} {context.Request.Path}");
    await next();
    Console.WriteLine($"Outgoing response: {context.Response.StatusCode} for {context.Request.Path}");
});

// DELETE endpoint logging loop:
var endpointDataSource = app.Services.GetRequiredService<EndpointDataSource>();
foreach (var endpoint in endpointDataSource.Endpoints)
{
    if (endpoint is RouteEndpoint routeEndpoint)
    {
        Console.WriteLine($"Mapped endpoint: {routeEndpoint.RoutePattern.RawText} -> {routeEndpoint.DisplayName}");
    }
}
```

`UseSerilogRequestLogging()` already handles request/response logging.

---

## 4.3 — Simplify Web Program.cs

### Updated File: `src/InfoDumpManager.Web/Program.cs`

Replace the duplicated registration block with the shared extension methods:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

// Shared registrations
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Web-specific services
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.Configure<WebUserContextOptions>(builder.Configuration.GetSection("WebUserContext"));
builder.Services.AddScoped<ICurrentUserContext, WebCurrentUserContext>();

var app = builder.Build();

// ... (keep existing pipeline configuration unchanged)
```

All the database, repository, agent, Redis, Polly, MediatR, and AutoMapper lines are now provided by `AddInfrastructure` and `AddApplication`.

---

## 4.4 — Fix CurrentUserContext Error Handling

### Problem
`CurrentUserContext.GetClaimValue` throws `InvalidOperationException` when a claim is missing, producing a 500 instead of 401.

### Updated File: `src/InfoDumpManager.WebAPI/Services/CurrentUserContext.cs`

```csharp
using System;
using System.Security.Authentication;
using InfoDumpManager.Application.Common.Services;
using Microsoft.AspNetCore.Http;

namespace InfoDumpManager.WebAPI.Services;

public sealed class CurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid UserId => GetClaimValue("sub");

    public Guid TenantId => GetClaimValue("tenant_id");

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    private Guid GetClaimValue(string claimType)
    {
        var claimValue = _httpContextAccessor.HttpContext?.User?.FindFirst(claimType)?.Value;
        if (Guid.TryParse(claimValue, out var parsed) && parsed != Guid.Empty)
        {
            return parsed;
        }

        throw new AuthenticationException($"Missing or invalid {claimType} claim.");
    }
}
```

Then update `ErrorHandlingMiddleware` to handle `AuthenticationException`:

```csharp
catch (System.Security.Authentication.AuthenticationException authException)
{
    Log.Warning(authException, "Authentication failure for {Path}", context.Request.Path);
    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
    context.Response.ContentType = "application/problem+json";

    var problemDetails = new ProblemDetails
    {
        Status = (int)HttpStatusCode.Unauthorized,
        Title = "Authentication required.",
        Detail = authException.Message,
        Instance = context.Request.Path
    };

    var payload = JsonSerializer.Serialize(problemDetails, SerializerOptions);
    await context.Response.WriteAsync(payload);
}
```

Place this `catch` block **before** the generic `catch (Exception)` block in the middleware.

---

## Phase 4 Completion Checklist

- [x] `DependencyInjection.cs` created in Application project with `AddApplication()` extension method.
- [x] `DependencyInjection.cs` created in Infrastructure project with `AddInfrastructure(IConfiguration)` extension method.
- [x] `DomainEventDispatchInterceptor` registered and wired into `AddDbContext`.
- [x] WebAPI `Program.cs` simplified to use `AddApplication()` + `AddInfrastructure()`.
- [x] Web `Program.cs` simplified to use `AddApplication()` + `AddInfrastructure()`.
- [x] All `Console.WriteLine` calls removed from WebAPI pipeline.
- [x] `CurrentUserContext` throws `AuthenticationException` instead of `InvalidOperationException`.
- [x] `ErrorHandlingMiddleware` handles `AuthenticationException` → 401, `ValidationException` → 400.
- [x] `dotnet build` succeeds.
- [x] `dotnet test` passes all existing tests.
