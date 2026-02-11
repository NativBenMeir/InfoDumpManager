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

        services.AddScoped<IGEMRepository, GEMRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<ICategorySuggestionRepository, CategorySuggestionRepository>();
        services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
        services.AddScoped<ICostUsageRepository, CostUsageRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<ICostManager, CostManagerImpl>();
        services.Configure<CostManagementOptions>(configuration.GetSection("CostManagement"));

        services.Configure<LLMRateLimitOptions>(configuration.GetSection("LLMRateLimit"));
        services.AddSingleton<Kernel>(_ => Kernel.CreateBuilder().Build());
        services.AddSingleton<ILLMProvider, SemanticKernelProvider>();
        services.AddSingleton<ILLMRateLimiter, TenantRateLimiter>();
        services.AddScoped<IEmbeddingProvider, DeterministicEmbeddingProvider>();
        services.AddScoped<IVectorStore, PostgreSqlVectorStore>();

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisConfiguration = configuration.GetConnectionString("Redis")
                ?? configuration["Redis:Configuration"]
                ?? "localhost:6379";
            return ConnectionMultiplexer.Connect(redisConfiguration);
        });
        services.AddSingleton<IEmbeddingCache, RedisEmbeddingCache>();
        services.AddSingleton<ITextCache, RedisTextCache>();

        services.Configure<MinioOptions>(configuration.GetSection("Minio"));
        services.AddScoped<IStorageService, MinioStorageService>();
        services.Configure<WebScrapingOptions>(configuration.GetSection("WebScraping"));
        services.AddScoped<IWebScrapingService, WebScrapingService>();

        services.AddScoped<IAgent, SummarizationAgent>();
        services.AddScoped<IAgent, CategorizationAgent>();
        services.AddScoped<IAgent, TaggingAgent>();
        services.AddScoped<IAgent, ValidationAgent>();

        services.AddSingleton<IJobTracker, InMemoryJobTracker>();
        services.AddSingleton<IJobQueue<ProcessingJob>, InMemoryJobQueue<ProcessingJob>>();
        services.AddSingleton<IContentProcessingOrchestrator, ContentProcessingOrchestrator>();
        services.AddHostedService<ContentProcessingBackgroundService>();

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
