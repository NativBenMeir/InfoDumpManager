using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using InfoDumpManager.Application;
using InfoDumpManager.Application.Agents;
using InfoDumpManager.Application.Agents.Implementations;
using InfoDumpManager.Application.Agents.Orchestration;
using InfoDumpManager.Application.Common.Services;
using InfoDumpManager.Application.Infrastructure.JobQueue;
using InfoDumpManager.Application.Services;
using InfoDumpManager.Application.Services.Caching;
using InfoDumpManager.Application.Services.CostManagement;
using InfoDumpManager.Application.Services.Embeddings;
using InfoDumpManager.Application.Services.LLM;
using InfoDumpManager.Application.Validators;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Infrastructure.Data;
using InfoDumpManager.Infrastructure.Repositories;
using InfoDumpManager.Infrastructure.Services.Caching;
using InfoDumpManager.Infrastructure.Services.Embeddings;
using InfoDumpManager.Infrastructure.Services.LLM;
using InfoDumpManager.Infrastructure.Services;
using InfoDumpManager.WebAPI.Middleware;
using InfoDumpManager.WebAPI.Options;
using InfoDumpManager.WebAPI.Services;
using InfoDumpManager.WebAPI.Validators.Auth;
using InfoDumpManager.WebAPI.Validators.Categories;
using InfoDumpManager.WebAPI.Validators.GEMs;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi.Models;
using Npgsql;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.SemanticKernel;
using Polly;
using Serilog;
using StackExchange.Redis;

namespace InfoDumpManager.WebAPI;

public class Program
{
    public static void Main(string[] args)
    {
        Log.Logger = BuildLogger();

        try
        {
            Log.Information("Starting InfoDumpManager WebAPI");
            var builder = WebApplication.CreateBuilder(args);
            
            builder.Host.UseSerilog();
            
            ConfigureServices(builder.Configuration, builder.Services);
            
            var app = builder.Build();
            
            ConfigurePipeline(app);
            
            app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static IConfigurationBuilder CreateConfigurationBuilder()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true);
    }

    private static Serilog.ILogger BuildLogger()
    {
        var configuration = CreateConfigurationBuilder().Build();
        return new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "InfoDumpManager.WebAPI")
            .CreateLogger();
    }

    private static void ConfigureServices(IConfiguration configuration, IServiceCollection services)
    {
        Console.WriteLine("ConfigureServices invoked");
        var jwtSettings = BuildJwtSettings(configuration);
        services.AddSingleton(jwtSettings);

        services.AddControllers()
            .AddJsonOptions(opts => opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Version = "v1",
                Title = "InfoDumpManager API",
                Description = "GEM ingestion, summarization, and categorization"
            });

            var bearerScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT Authorization header using the Bearer scheme."
            };

            options.AddSecurityDefinition("Bearer", bearerScheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [bearerScheme] = Array.Empty<string>()
            });
        });

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? Environment.GetEnvironmentVariable("CONNECTION_STR");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("The default connection string is not configured.");
        }

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector();
        var dataSource = dataSourceBuilder.Build();

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(dataSource, sql => {
                sql.EnableRetryOnFailure();
                sql.UseVector();
            }));

        services.AddIdentity<User, IdentityRole<Guid>>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret));

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey
                };
            });

        services.PostConfigure<AuthenticationOptions>(options =>
        {
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("MultiTenant", policy => policy.RequireClaim("tenant_id"));
        });

        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddScoped<ICurrentUserContext, CurrentUserContext>();
        services.AddScoped<ITokenService, JwtTokenService>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IGEMRepository, GEMRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<ICategorySuggestionRepository, CategorySuggestionRepository>();
        services.AddScoped<ICostUsageRepository, CostUsageRepository>();
        services.AddScoped<ICostManager, CostManagerImpl>();
        services.Configure<CostManagementOptions>(configuration.GetSection("CostManagement"));
        services.Configure<LLMRateLimitOptions>(configuration.GetSection("LLMRateLimit"));
        services.Configure<MinioOptions>(configuration.GetSection("Minio"));
        services.Configure<WebScrapingOptions>(configuration.GetSection("WebScraping"));
        services.AddScoped<IStorageService, MinioStorageService>();
        services.AddScoped<IWebScrapingService, WebScrapingService>();

        services.AddSingleton<IJobQueue<ProcessingJob>, InMemoryJobQueue<ProcessingJob>>();
        services.AddSingleton<IContentProcessingOrchestrator, ContentProcessingOrchestrator>();
        services.AddHostedService<ContentProcessingBackgroundService>();

        services.AddScoped<IAgent, SummarizationAgent>();
        services.AddScoped<IAgent, CategorizationAgent>();
        services.AddScoped<IAgent, TaggingAgent>();
        services.AddScoped<IAgent, ValidationAgent>();

        services.AddSingleton<Kernel>(_ => Kernel.CreateBuilder().Build());
        services.AddSingleton<ILLMProvider, SemanticKernelProvider>();
        services.AddSingleton<ILLMRateLimiter, TenantRateLimiter>();
        services.AddScoped<IEmbeddingProvider, DeterministicEmbeddingProvider>();
        services.AddScoped<IVectorStore, PostgreSqlVectorStore>();
        services.AddSingleton<IEmbeddingCache, RedisEmbeddingCache>();
        services.AddSingleton<ITextCache, RedisTextCache>();
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisConfiguration = configuration.GetConnectionString("Redis")
                ?? configuration["Redis:Configuration"]
                ?? "localhost:6379";
            return ConnectionMultiplexer.Connect(redisConfiguration);
        });

        var retryPolicy = Policy.Handle<Exception>().WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));
        var breakerPolicy = Policy.Handle<Exception>().CircuitBreakerAsync(2, TimeSpan.FromSeconds(30));
        var databasePolicy = Policy.WrapAsync(retryPolicy, breakerPolicy);

        services.AddSingleton<IAsyncPolicy>(databasePolicy);
        services.AddSingleton<IDatabasePolicy>(sp => new PollyDatabasePolicy(sp.GetRequiredService<IAsyncPolicy>()));

        services.AddAutoMapper(typeof(AssemblyReference).Assembly);
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(typeof(AssemblyReference).Assembly);
        });

        services.AddFluentValidationAutoValidation();
        services.AddValidatorsFromAssemblyContaining<CreateGEMCommandValidator>();
        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
        services.AddValidatorsFromAssemblyContaining<CreateGemRequestValidator>();
        services.AddValidatorsFromAssemblyContaining<CreateCategoryRequestValidator>();
    }

    private static void ConfigurePipeline(WebApplication app)
    {
        var env = app.Environment;
        Console.WriteLine($"Configuring pipeline for {env.EnvironmentName}");

        if (env.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseMiddleware<ErrorHandlingMiddleware>();
        app.Use(async (context, next) =>
        {
            Console.WriteLine($"Incoming request: {context.Request.Method} {context.Request.Path}");
            await next();
            Console.WriteLine($"Outgoing response: {context.Response.StatusCode} for {context.Request.Path}");
        });
        app.UseSerilogRequestLogging();

        app.UseHttpsRedirection();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        var endpointDataSource = app.Services.GetRequiredService<EndpointDataSource>();
        foreach (var endpoint in endpointDataSource.Endpoints)
        {
            if (endpoint is RouteEndpoint routeEndpoint)
            {
                Console.WriteLine($"Mapped endpoint: {routeEndpoint.RoutePattern.RawText} -> {routeEndpoint.DisplayName}");
            }
        }
    }

    private static JwtSettings BuildJwtSettings(IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection("JwtSettings");
        var jwtSettings = jwtSection.Get<JwtSettings>() ?? new JwtSettings();

        var secretOverride = Environment.GetEnvironmentVariable("JWT_SECRET");
        if (!string.IsNullOrWhiteSpace(secretOverride))
        {
            jwtSettings = jwtSettings with { Secret = secretOverride };
        }

        if (string.IsNullOrWhiteSpace(jwtSettings.Secret))
        {
            throw new InvalidOperationException("JWT secret must be configured via JwtSettings:Secret or JWT_SECRET environment variable.");
        }

        if (string.IsNullOrWhiteSpace(jwtSettings.Issuer) || string.IsNullOrWhiteSpace(jwtSettings.Audience))
        {
            throw new InvalidOperationException("JwtSettings must specify Issuer and Audience.");
        }

        return jwtSettings;
    }
}
