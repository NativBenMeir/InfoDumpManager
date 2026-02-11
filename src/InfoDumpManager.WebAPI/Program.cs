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
using InfoDumpManager.Application.Common.Behaviors;
using InfoDumpManager.Application.Common.Services;
using InfoDumpManager.Application.Infrastructure.JobQueue;
using InfoDumpManager.Application.Services.Caching;
using InfoDumpManager.Application.Services.CostManagement;
using InfoDumpManager.Application.Services.Embeddings;
using InfoDumpManager.Application.Services.LLM;
using InfoDumpManager.Application.Services.Storage;
using InfoDumpManager.Application.Validators;
using InfoDumpManager.Infrastructure;
using InfoDumpManager.Infrastructure.Data.Entities;
using InfoDumpManager.Infrastructure.Data;
using InfoDumpManager.Infrastructure.Services;
using InfoDumpManager.WebAPI.Middleware;
using InfoDumpManager.WebAPI.Options;
using InfoDumpManager.WebAPI.Services;
using InfoDumpManager.WebAPI.Validators.Auth;
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

        services.AddApplication();
        services.AddInfrastructure(configuration);

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

        services.AddFluentValidationAutoValidation();
        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
    }

    private static void ConfigurePipeline(WebApplication app)
    {
        var env = app.Environment;

        if (env.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseMiddleware<ErrorHandlingMiddleware>();
        app.UseSerilogRequestLogging();

        app.UseHttpsRedirection();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
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
