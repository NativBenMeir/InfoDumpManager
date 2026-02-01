using System;
using InfoDumpManager.Application;
using InfoDumpManager.Application.Common.Services;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Infrastructure.Data;
using InfoDumpManager.Infrastructure.Repositories;
using InfoDumpManager.Infrastructure.Services;
using InfoDumpManager.Web.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Polly;

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
    options.UseNpgsql(connectionString, sql => sql.EnableRetryOnFailure()));

builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.Configure<WebUserContextOptions>(builder.Configuration.GetSection("WebUserContext"));
builder.Services.AddScoped<ICurrentUserContext, WebCurrentUserContext>();

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IGEMRepository, GEMRepository>();

builder.Services.Configure<WebScrapingOptions>(builder.Configuration.GetSection("WebScraping"));
builder.Services.AddScoped<IWebScrapingService, WebScrapingService>();

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
