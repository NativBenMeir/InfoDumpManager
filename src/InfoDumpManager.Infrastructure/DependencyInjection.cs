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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Npgsql;
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
        services.AddOptions<AgentLlmSettings>()
            .Bind(configuration.GetSection("LLM"));
        services.AddSingleton<IValidateOptions<AgentLlmSettings>, AgentLlmSettingsValidator>();
        services.AddOptions<AgentLlmSettings>()
            .ValidateOnStart();

        services.AddSingleton<IReadOnlyDictionary<string, Kernel>>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("SemanticKernel");
            var settings = sp.GetRequiredService<IOptions<AgentLlmSettings>>().Value;

            var providers = settings.Agents
                .SelectMany(x => new[] { x.Value.Chat.Provider, x.Value.Embedding.Provider })
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var kernels = new Dictionary<string, Kernel>(StringComparer.OrdinalIgnoreCase);
            foreach (var provider in providers)
            {
                var kernel = CreateKernelForProvider(provider, config);
                kernels[provider] = kernel;
                logger.LogInformation("Registered LLM provider kernel for {Provider}", provider);
            }

            return kernels;
        });
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
        services.AddScoped<IHtmlContentExtractor, AngleSharpHtmlContentExtractor>();

        services.AddScoped<IAgent, SummarizationAgent>();
        services.AddScoped<IAgent, CategorizationAgent>();
        services.AddScoped<IAgent, TaggingAgent>();
        services.AddScoped<IAgent, ValidationAgent>();

        var useRedisJobs = configuration.GetValue<bool>("JobQueue:UseRedis", true);
        if (useRedisJobs)
        {
            services.AddSingleton<IJobTracker, RedisJobTracker>();
            services.AddSingleton<IJobQueue<ProcessingJob>, RedisJobQueue<ProcessingJob>>();
        }
        else
        {
            services.AddSingleton<IJobTracker, InMemoryJobTracker>();
            services.AddSingleton<IJobQueue<ProcessingJob>, InMemoryJobQueue<ProcessingJob>>();
        }

        services.AddSingleton<IContentProcessingOrchestrator, ContentProcessingOrchestrator>();
        services.AddScoped<IProcessingPersistence, ProcessingPersistence>();
        services.AddScoped<IProcessingActivityLogger, ProcessingActivityLogger>();
        services.AddHostedService<ContentProcessingBackgroundService>();

        services.AddSingleton<IResiliencePolicyProvider, PollyResiliencePolicyProvider>();
        services.AddSingleton<IDatabasePolicy, PollyDatabasePolicy>();

        return services;
    }

    private static Kernel CreateKernelForProvider(string provider, IConfiguration configuration)
    {
        var builder = Kernel.CreateBuilder();
        var model = configuration["LLM:Model"];

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException("LLM:Model must be configured.");
        }

        if (provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            var openAiKey = configuration["LLM:OpenAI:ApiKey"];
            if (string.IsNullOrWhiteSpace(openAiKey))
            {
                throw new InvalidOperationException("LLM:OpenAI:ApiKey must be configured when provider OpenAI is used.");
            }

            builder.AddOpenAIChatCompletion(model, openAiKey);
            return builder.Build();
        }

        if (provider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase))
        {
            var azureEndpoint = configuration["LLM:AzureOpenAI:Endpoint"];
            var azureKey = configuration["LLM:AzureOpenAI:ApiKey"];

            if (string.IsNullOrWhiteSpace(azureEndpoint) || string.IsNullOrWhiteSpace(azureKey))
            {
                throw new InvalidOperationException(
                    "LLM:AzureOpenAI:Endpoint and LLM:AzureOpenAI:ApiKey must be configured when provider AzureOpenAI is used.");
            }

            builder.AddAzureOpenAIChatCompletion(model, azureEndpoint, azureKey);
            return builder.Build();
        }

        throw new InvalidOperationException($"Unsupported LLM provider '{provider}'. Supported values: OpenAI, AzureOpenAI.");
    }
}
