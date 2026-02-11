using System;
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
using InfoDumpManager.Infrastructure;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Infrastructure.Data;
using InfoDumpManager.Infrastructure.Repositories;
using InfoDumpManager.Infrastructure.Services.Caching;
using InfoDumpManager.Infrastructure.Services.Embeddings;
using InfoDumpManager.Infrastructure.Services.LLM;
using InfoDumpManager.Infrastructure.Services;
using InfoDumpManager.Web.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Npgsql;
using Polly;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.Configure<WebUserContextOptions>(builder.Configuration.GetSection("WebUserContext"));
builder.Services.AddScoped<ICurrentUserContext, WebCurrentUserContext>();

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
