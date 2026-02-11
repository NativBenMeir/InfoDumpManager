# Phase 6 — Polly Centralization & Semantic Kernel Configuration

## Goal
1. Centralize all Polly resilience policies into a single provider so they aren't duplicated across DB and LLM code.
2. Configure Semantic Kernel with actual AI service registration (or a clear placeholder pattern) so `InvokePromptAsync` doesn't fail at runtime with an empty kernel.
3. Add empty Domain Services folder context (out of scope for code changes, noted for documentation).

## Current State

### Polly duplication

**Database policy** is defined in `src/InfoDumpManager.Infrastructure/DependencyInjection.cs`:
```csharp
var retryPolicy = Policy.Handle<Exception>()
    .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));
var breakerPolicy = Policy.Handle<Exception>()
    .CircuitBreakerAsync(2, TimeSpan.FromSeconds(30));
var databasePolicy = Policy.WrapAsync(retryPolicy, breakerPolicy);

services.AddSingleton<IAsyncPolicy>(databasePolicy);
services.AddSingleton<IDatabasePolicy>(sp =>
    new PollyDatabasePolicy(sp.GetRequiredService<IAsyncPolicy>()));
```

**LLM policy** is defined inline in `src/InfoDumpManager.Infrastructure/Services/LLM/SemanticKernelProvider.cs`:
```csharp
private IAsyncPolicy<LLMResponse> BuildPolicy()
{
    var retryPolicy = Policy<LLMResponse>
        .Handle<Exception>()
        .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)), ...);

    var circuitBreaker = Policy<LLMResponse>
        .Handle<Exception>()
        .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

    return Policy.WrapAsync(retryPolicy, circuitBreaker);
}
```

### Semantic Kernel configuration

**File:** `src/InfoDumpManager.Infrastructure/DependencyInjection.cs`
```csharp
services.AddSingleton<Kernel>(_ => Kernel.CreateBuilder().Build());
```

The kernel is empty — no AI services registered. Calling `InvokePromptAsync` will throw at runtime.

## Changes

### 6.1 — Create `IResiliencePolicyProvider` interface

**New file:** `src/InfoDumpManager.Application/Common/Services/IResiliencePolicyProvider.cs`

```csharp
using Polly;

namespace InfoDumpManager.Application.Common.Services;

/// <summary>
/// Centralized provider for resilience policies.
/// </summary>
public interface IResiliencePolicyProvider
{
    /// <summary>
    /// Non-generic async policy for database operations.
    /// </summary>
    IAsyncPolicy DatabasePolicy { get; }

    /// <summary>
    /// Typed async policy for LLM operations.
    /// </summary>
    IAsyncPolicy<T> GetLLMPolicy<T>();
}
```

### 6.2 — Create `PollyResiliencePolicyProvider` implementation

**New file:** `src/InfoDumpManager.Infrastructure/Services/PollyResiliencePolicyProvider.cs`

```csharp
using InfoDumpManager.Application.Common.Services;
using Microsoft.Extensions.Logging;
using Polly;

namespace InfoDumpManager.Infrastructure.Services;

/// <summary>
/// Central Polly policy provider for all resilience scenarios.
/// </summary>
public sealed class PollyResiliencePolicyProvider : IResiliencePolicyProvider
{
    private readonly ILogger<PollyResiliencePolicyProvider> _logger;

    public PollyResiliencePolicyProvider(ILogger<PollyResiliencePolicyProvider> logger)
    {
        _logger = logger;

        // Database: retry 3x with exponential back-off + circuit breaker (2 failures / 30s)
        var dbRetry = Policy.Handle<Exception>()
            .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                (ex, delay, attempt, _) =>
                    _logger.LogWarning(ex, "Database retry {Attempt} after {Delay}", attempt, delay));

        var dbBreaker = Policy.Handle<Exception>()
            .CircuitBreakerAsync(2, TimeSpan.FromSeconds(30),
                (ex, duration) => _logger.LogWarning(ex, "Database circuit opened for {Duration}", duration),
                () => _logger.LogInformation("Database circuit closed"));

        DatabasePolicy = Policy.WrapAsync(dbRetry, dbBreaker);
    }

    public IAsyncPolicy DatabasePolicy { get; }

    public IAsyncPolicy<T> GetLLMPolicy<T>()
    {
        // LLM: retry 3x with exponential back-off + circuit breaker (5 failures / 30s)
        var retry = Policy<T>.Handle<Exception>()
            .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                (outcome, delay, attempt, _) =>
                {
                    if (outcome.Exception is not null)
                        _logger.LogWarning(outcome.Exception, "LLM retry {Attempt} after {Delay}", attempt, delay);
                    else
                        _logger.LogWarning("LLM retry {Attempt} after {Delay}", attempt, delay);
                });

        var breaker = Policy<T>.Handle<Exception>()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

        return Policy.WrapAsync(retry, breaker);
    }
}
```

### 6.3 — Update `SemanticKernelProvider` to use centralized policy

**File:** `src/InfoDumpManager.Infrastructure/Services/LLM/SemanticKernelProvider.cs`

1. Remove the `BuildPolicy()` private method entirely.
2. Accept `IResiliencePolicyProvider` via constructor injection.
3. Initialize `_policy` from the provider.

**Updated constructor:**
```csharp
public sealed class SemanticKernelProvider : ILLMProvider
{
    private readonly Kernel _kernel;
    private readonly ILogger<SemanticKernelProvider> _logger;
    private readonly IAsyncPolicy<LLMResponse> _policy;

    public SemanticKernelProvider(
        Kernel kernel,
        IResiliencePolicyProvider resilienceProvider,
        ILogger<SemanticKernelProvider> logger)
    {
        _kernel = kernel;
        _logger = logger;
        _policy = resilienceProvider.GetLLMPolicy<LLMResponse>();
    }

    // ... rest unchanged, remove BuildPolicy()
}
```

### 6.4 — Update `PollyDatabasePolicy` to use centralized policy

**File:** `src/InfoDumpManager.Infrastructure/Services/PollyDatabasePolicy.cs`

Change constructor to accept `IResiliencePolicyProvider` instead of raw `IAsyncPolicy`:

```csharp
public sealed class PollyDatabasePolicy : IDatabasePolicy
{
    private readonly IAsyncPolicy _policy;

    public PollyDatabasePolicy(IResiliencePolicyProvider resilienceProvider)
    {
        _policy = resilienceProvider.DatabasePolicy;
    }

    public Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken = default)
        => _policy.ExecuteAsync(_ => action(), cancellationToken);

    public Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
        => _policy.ExecuteAsync(_ => action(), cancellationToken);
}
```

### 6.5 — Update DI registration

**File:** `src/InfoDumpManager.Infrastructure/DependencyInjection.cs`

**Remove** the inline policy construction (the `var retryPolicy = ...` / `var breakerPolicy = ...` / `var databasePolicy = ...` block and the two `services.AddSingleton` calls for `IAsyncPolicy` and `IDatabasePolicy`).

**Replace with:**
```csharp
// Centralized resilience policies
services.AddSingleton<IResiliencePolicyProvider, PollyResiliencePolicyProvider>();
services.AddSingleton<IDatabasePolicy, PollyDatabasePolicy>();
```

The `IDatabasePolicy` registration now internally uses `IResiliencePolicyProvider`.
The `SemanticKernelProvider` registration is unchanged (`AddSingleton<ILLMProvider, SemanticKernelProvider>`) — it will receive `IResiliencePolicyProvider` via DI.

Remove `services.AddSingleton<IAsyncPolicy>(databasePolicy)` — no longer needed.

### 6.6 — Configure Semantic Kernel with AI service

**File:** `src/InfoDumpManager.Infrastructure/DependencyInjection.cs`

Replace the empty kernel builder:
```csharp
services.AddSingleton<Kernel>(_ => Kernel.CreateBuilder().Build());
```

With a conditional registration that reads configuration:
```csharp
services.AddSingleton<Kernel>(sp =>
{
    var builder = Kernel.CreateBuilder();
    var config = sp.GetRequiredService<IConfiguration>();

    var openAiKey = config["LLM:OpenAI:ApiKey"]
        ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    var azureEndpoint = config["LLM:AzureOpenAI:Endpoint"];
    var azureKey = config["LLM:AzureOpenAI:ApiKey"];
    var model = config["LLM:Model"] ?? "gpt-4";

    if (!string.IsNullOrWhiteSpace(azureEndpoint) && !string.IsNullOrWhiteSpace(azureKey))
    {
        builder.AddAzureOpenAIChatCompletion(model, azureEndpoint, azureKey);
    }
    else if (!string.IsNullOrWhiteSpace(openAiKey))
    {
        builder.AddOpenAIChatCompletion(model, openAiKey);
    }
    else
    {
        // No AI service configured — InvokePromptAsync will throw.
        // Log a warning so operators know to configure LLM credentials.
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("SemanticKernel");
        logger.LogWarning("No LLM provider configured. Set LLM:OpenAI:ApiKey or LLM:AzureOpenAI:Endpoint + ApiKey.");
    }

    return builder.Build();
});
```

> **Note:** This requires adding `using Microsoft.Extensions.Logging;` and `using Microsoft.Extensions.Configuration;` if not already present. Both are already imported in the file.

> **NuGet dependency:** `AddOpenAIChatCompletion` and `AddAzureOpenAIChatCompletion` come from `Microsoft.SemanticKernel.Connectors.OpenAI`. If not already in the project, run:
> ```bash
> dotnet add src/InfoDumpManager.Infrastructure package Microsoft.SemanticKernel.Connectors.OpenAI
> ```
> Check if this package is already referenced — `Microsoft.SemanticKernel` may include it transitively.

### 6.7 — Add configuration section

**File:** `src/InfoDumpManager.WebAPI/appsettings.json`

Add:
```json
"LLM": {
    "Model": "gpt-4",
    "OpenAI": {
        "ApiKey": ""
    },
    "AzureOpenAI": {
        "Endpoint": "",
        "ApiKey": ""
    }
}
```

**File:** `src/InfoDumpManager.WebAPI/appsettings.Development.json`

Override with actual dev credentials (or keep empty and use environment variables):
```json
"LLM": {
    "Model": "gpt-4",
    "OpenAI": {
        "ApiKey": ""
    }
}
```

### 6.8 — Domain Services folder (documentation only)

The review noted `Domain/Services/` is empty. This is not a code defect — it's an architectural note that domain services can be added as cross-aggregate business rules emerge. **No code changes.**

Add a placeholder `README.md` if desired:

**New file:** `src/InfoDumpManager.Domain/Services/README.md`
```markdown
# Domain Services

This folder will contain domain services for cross-aggregate business rules.

Examples of future domain services:
- `GEMDuplicateChecker` — checks if a URL already exists within tenant scope
- `CategoryReassignmentPolicy` — validates whether a GEM can be moved between categories
```

## Verification

```bash
dotnet build
dotnet test
```

Verify no inline policy construction remains:
```bash
grep -n "BuildPolicy" src/InfoDumpManager.Infrastructure/Services/LLM/SemanticKernelProvider.cs
# Should have zero matches

grep -n "Policy.Handle" src/InfoDumpManager.Infrastructure/DependencyInjection.cs
# Should have zero matches
```

Verify Semantic Kernel is conditionally configured:
```bash
grep -n "AddOpenAIChatCompletion\|AddAzureOpenAIChatCompletion" src/InfoDumpManager.Infrastructure/DependencyInjection.cs
# Should have matches
```
