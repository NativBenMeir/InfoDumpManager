using System;
using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using InfoDumpManager.Application.Services;
using InfoDumpManager.Application.Validators;
using InfoDumpManager.Domain.Common;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Infrastructure.Configuration;
using InfoDumpManager.Infrastructure.Data;
using InfoDumpManager.Infrastructure.Repositories;
using InfoDumpManager.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

// Add database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Add Identity
builder.Services.AddIdentity<IdentityUser<Guid>, IdentityRole<Guid>>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Configure Identity options
builder.Services.Configure<IdentityOptions>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
});

// Add JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"] 
    ?? throw new InvalidOperationException("JWT secret key not configured");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "InfoDumpManager";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "InfoDumpManager-API";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// Add repositories
builder.Services.AddScoped<IGEMRepository, GEMRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Add MediatR
builder.Services.AddMediatR(typeof(Program).Assembly, typeof(InfoDumpManager.Application.AssemblyMarker).Assembly);

// Add AutoMapper
builder.Services.AddAutoMapper(typeof(Program).Assembly, typeof(InfoDumpManager.Application.AssemblyMarker).Assembly);

// Add FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<CreateGemCommandValidator>();
builder.Services.AddFluentValidationAutoValidation();

builder.Services.Configure<MinioOptions>(builder.Configuration.GetSection("Minio"));
builder.Services.AddSingleton<IPageSnapshotService, PlaywrightPageSnapshotService>();
builder.Services.AddSingleton<ISnapshotStorageService, MinioSnapshotStorageService>();

// Add API services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Apply migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
