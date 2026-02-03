using System;
using InfoDumpManager.Application;
using InfoDumpManager.Application.Agents;
using InfoDumpManager.Application.Agents.Implementations;
using InfoDumpManager.Application.Agents.Orchestration;
using InfoDumpManager.Application.Common.Services;
using InfoDumpManager.Application.Infrastructure.JobQueue;
using InfoDumpManager.Application.Services;
using InfoDumpManager.Application.Services.CostManagement;
using InfoDumpManager.Application.Services.Embeddings;
using InfoDumpManager.Application.Services.LLM;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Infrastructure.Data;
using InfoDumpManager.Infrastructure.Repositories;
using InfoDumpManager.Infrastructure.Services.Embeddings;
using InfoDumpManager.Infrastructure.Services.LLM;
using InfoDumpManager.Infrastructure.Services;
using InfoDumpManager.Web.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Polly;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("CONNECTION_STR");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("The default connection string is not configured.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString, sql => {
        sql.EnableRetryOnFailure();
        sql.UseVector();
    }));

builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.Configure<WebUserContextOptions>(builder.Configuration.GetSection("WebUserContext"));
builder.Services.AddScoped<ICurrentUserContext, WebCurrentUserContext>();

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IGEMRepository, GEMRepository>();
builder.Services.AddScoped<ICostUsageRepository, CostUsageRepository>();
builder.Services.AddScoped<ICostManager, CostManagerImpl>();
builder.Services.Configure<CostManagementOptions>(builder.Configuration.GetSection("CostManagement"));

builder.Services.Configure<WebScrapingOptions>(builder.Configuration.GetSection("WebScraping"));
builder.Services.AddScoped<IWebScrapingService, WebScrapingService>();

builder.Services.AddSingleton<IJobQueue<ProcessingJob>, InMemoryJobQueue<ProcessingJob>>();
builder.Services.AddSingleton<IContentProcessingOrchestrator, ContentProcessingOrchestrator>();
builder.Services.AddHostedService<ContentProcessingBackgroundService>();

builder.Services.AddScoped<IAgent, SummarizationAgent>();
builder.Services.AddScoped<IAgent, CategorizationAgent>();
builder.Services.AddScoped<IAgent, TaggingAgent>();
builder.Services.AddScoped<IAgent, ValidationAgent>();

builder.Services.AddSingleton<Kernel>(_ => Kernel.CreateBuilder().Build());
builder.Services.AddSingleton<ILLMProvider, SemanticKernelProvider>();
builder.Services.AddScoped<IEmbeddingProvider, DeterministicEmbeddingProvider>();
builder.Services.AddScoped<IVectorStore, PostgreSqlVectorStore>();
builder.Services.AddSingleton<IEmbeddingCache, RedisEmbeddingCache>();
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var redisConfiguration = builder.Configuration.GetConnectionString("Redis")
        ?? builder.Configuration["Redis:Configuration"]
        ?? "localhost:6379";
    return ConnectionMultiplexer.Connect(redisConfiguration);
});

var retryPolicy = Policy.Handle<Exception>().WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));
var breakerPolicy = Policy.Handle<Exception>().CircuitBreakerAsync(2, TimeSpan.FromSeconds(30));
var databasePolicy = Policy.WrapAsync(retryPolicy, breakerPolicy);

builder.Services.AddSingleton<IAsyncPolicy>(databasePolicy);
builder.Services.AddSingleton<IDatabasePolicy>(sp => new PollyDatabasePolicy(sp.GetRequiredService<IAsyncPolicy>()));

builder.Services.AddAutoMapper(typeof(AssemblyReference).Assembly);
builder.Services.AddMediatR(configuration =>
{
    configuration.RegisterServicesFromAssembly(typeof(AssemblyReference).Assembly);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();
