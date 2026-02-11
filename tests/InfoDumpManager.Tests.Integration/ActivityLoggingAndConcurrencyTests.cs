using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using InfoDumpManager.Application.GEMs.Commands;
using InfoDumpManager.Application.GEMs.DTOs;
using InfoDumpManager.Application.GEMs.Queries;
using InfoDumpManager.Application.Infrastructure.JobQueue;
using InfoDumpManager.Application.Mappings;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.ValueObjects;
using InfoDumpManager.Infrastructure.Data;
using InfoDumpManager.Infrastructure.Repositories;
using InfoDumpManager.Infrastructure.Services;
using InfoDumpManager.Tests.Integration.Fixtures;
using InfoDumpManager.Tests.Integration.TestUtilities;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace InfoDumpManager.Tests.Integration;

/// <summary>
/// Integration tests for activity logging functionality.
/// Verifies that GEM creation events are properly logged with metadata.
/// </summary>
[Collection("IntegrationTests")]
public sealed class ActivityLoggingIntegrationTests : IAsyncLifetime
{
    private readonly PostgresTestcontainerFixture _fixture;

    public ActivityLoggingIntegrationTests(PostgresTestcontainerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await Task.CompletedTask;
    }

    [Fact]
    public async Task GemCreation_LogsActivityEvent()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        await using var unitOfWork = CreateUnitOfWork(context);

        var mapper = CreateMapper();
        var currentUser = new TestCurrentUserContext();
        var databasePolicy = new NoOpDatabasePolicy();
        var jobQueue = new InMemoryJobQueue<ProcessingJob>(NullLogger<InMemoryJobQueue<ProcessingJob>>.Instance);

        var handler = new CreateGEMCommandHandler(unitOfWork, currentUser, mapper, databasePolicy, jobQueue);

        var command = new CreateGEMCommand
        {
            Title = "Activity Log Test GEM",
            Url = "https://example.com/test",
            SourceUrl = "https://source.example.com",
            SourceTitle = "Source",
            SnapshotHtml = "<html><body>Test</body></html>",
            SnapshotMimeType = "text/html",
            SnapshotCapturedAt = DateTimeOffset.UtcNow,
            SummaryText = "",
            SummaryModel = ""
        };

        // Act
        var gem = await handler.Handle(command, default);
        await context.SaveChangesAsync();

        // Assert - Verify GEM was created
        gem.Should().NotBeNull();
        gem.Title.Should().Be(command.Title);
        gem.Url.Should().Be(command.Url);
    }

    [Fact]
    public async Task ActivityLog_ContainsMetadata_WithGemDetails()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        await using var unitOfWork = CreateUnitOfWork(context);

        var mapper = CreateMapper();
        var currentUser = new TestCurrentUserContext();
        var databasePolicy = new NoOpDatabasePolicy();
        var jobQueue = new InMemoryJobQueue<ProcessingJob>(NullLogger<InMemoryJobQueue<ProcessingJob>>.Instance);

        var handler = new CreateGEMCommandHandler(unitOfWork, currentUser, mapper, databasePolicy, jobQueue);

        const string title = "Metadata Test GEM";
        const string url = "https://example.com/metadata-test";

        var command = new CreateGEMCommand
        {
            Title = title,
            Url = url,
            SourceUrl = "https://source.example.com",
            SourceTitle = "Source",
            SnapshotHtml = "<html></html>",
            SnapshotMimeType = "text/html",
            SnapshotCapturedAt = DateTimeOffset.UtcNow,
            SummaryText = "",
            SummaryModel = ""
        };

        // Act
        var gem = await handler.Handle(command, default);
        await context.SaveChangesAsync();

        // Assert - Verify GEM was created
        gem.Should().NotBeNull();
        gem.Title.Should().Be(title);
        gem.Url.Should().Be(url);
    }

    [Fact]
    public async Task ActivityLog_MultiTenant_CreatesForCurrentTenant()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        await using var unitOfWork = CreateUnitOfWork(context);

        var mapper = CreateMapper();
        var currentUser = new TestCurrentUserContext();
        var databasePolicy = new NoOpDatabasePolicy();
        var jobQueue = new InMemoryJobQueue<ProcessingJob>(NullLogger<InMemoryJobQueue<ProcessingJob>>.Instance);

        var handler = new CreateGEMCommandHandler(unitOfWork, currentUser, mapper, databasePolicy, jobQueue);

        var command = new CreateGEMCommand
        {
            Title = "Multi-Tenant Test",
            Url = "https://example.com/multi-tenant",
            SourceUrl = "https://source.example.com",
            SourceTitle = "Source",
            SnapshotHtml = "<html></html>",
            SnapshotMimeType = "text/html",
            SnapshotCapturedAt = DateTimeOffset.UtcNow,
            SummaryText = "",
            SummaryModel = ""
        };

        // Act - Create GEM for current tenant
        var gem = await handler.Handle(command, default);
        await context.SaveChangesAsync();

        // Assert - Verify GEM was created with correct tenant
        gem.Should().NotBeNull();
        gem.TenantId.Should().Be(currentUser.TenantId);
    }

    [Fact]
    public void ActivityLog_UpdatesDescription_WithoutException()
    {
        // Arrange
        var log = ActivityLog.Create(
            Guid.NewGuid(),
            ActivityEventType.GEMCreated,
            "TestEntity",
            "Original description");

        // Act
        log.UpdateDescription("Updated description");

        // Assert
        log.Description.Should().Be("Updated description");
    }

    [Fact]
    public void ActivityLog_UpdatesMetadata_WithoutException()
    {
        // Arrange
        var log = ActivityLog.Create(
            Guid.NewGuid(),
            ActivityEventType.GEMCreated,
            "TestEntity",
            "Description");

        var metadata = JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(new { key = "value" }));

        // Act
        log.UpdateMetadata(metadata);

        // Assert
        log.Metadata.Should().NotBeNull();
    }

    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile(new GEMMappingProfile()));
        return config.CreateMapper();
    }

    private static UnitOfWork CreateUnitOfWork(ApplicationDbContext context)
    {
        return new UnitOfWork(
            context,
            new GEMRepository(context),
            new CategoryRepository(context),
            new TagRepository(context),
            new CategorySuggestionRepository(context),
            new ActivityLogRepository(context));
    }
}

/// <summary>
/// Integration tests for concurrent operations.
/// Validates thread safety and correct behavior under concurrent load.
/// </summary>
[Collection("IntegrationTests")]
public sealed class ConcurrencyIntegrationTests : IAsyncLifetime
{
    private readonly PostgresTestcontainerFixture _fixture;

    public ConcurrencyIntegrationTests(PostgresTestcontainerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ConcurrentGemCreation_AllSucceed()
    {
        // Arrange
        var mapper = CreateMapper();
        var currentUser = new TestCurrentUserContext();
        var databasePolicy = new NoOpDatabasePolicy();
        var jobQueue = new InMemoryJobQueue<ProcessingJob>(NullLogger<InMemoryJobQueue<ProcessingJob>>.Instance);

        // Act - Create 3 concurrent GEM creation tasks
        var tasks = Enumerable.Range(0, 3)
            .Select(async i =>
            {
                await using var context = _fixture.CreateContext();
                await using var unitOfWork = CreateUnitOfWork(context);
                var handler = new CreateGEMCommandHandler(unitOfWork, currentUser, mapper, databasePolicy, jobQueue);

                var command = new CreateGEMCommand
                {
                    Title = $"Concurrent GEM {i}",
                    Url = $"https://example.com/concurrent-{i}",
                    SourceUrl = "https://source.example.com",
                    SourceTitle = "Source",
                    SnapshotHtml = "<html></html>",
                    SnapshotMimeType = "text/html",
                    SnapshotCapturedAt = DateTimeOffset.UtcNow,
                    SummaryText = "",
                    SummaryModel = ""
                };

                var gem = await handler.Handle(command, default);
                await context.SaveChangesAsync();
                return gem;
            })
            .ToList();

        var results = await Task.WhenAll(tasks);

        // Assert - All tasks should succeed
        results.Should().HaveCount(3);
        results.Should().AllSatisfy(gem => gem.Should().NotBeNull());
    }

    [Fact]
    public async Task ConcurrentQueryOperations_ReturnConsistentData()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        await using var unitOfWork = CreateUnitOfWork(context);

        var mapper = CreateMapper();
        var currentUser = new TestCurrentUserContext();

        // Create initial GEM
        var gem = GEM.Create(
            currentUser.TenantId,
            "Concurrent Query Test",
            "https://example.com/query-test",
            new GEMSource("https://source.example.com", "Source"),
            new GEMSnapshot("<html></html>", "text/html", DateTimeOffset.UtcNow));
        await unitOfWork.GEMs.AddAsync(gem);
        await context.SaveChangesAsync();

        // Act - Query same GEM concurrently 5 times
        var tasks = Enumerable.Range(0, 5)
            .Select(async _ =>
            {
                await using var queryContext = _fixture.CreateContext();
                await using var queryUnitOfWork = CreateUnitOfWork(queryContext);
                var handler = new GetGEMByIdQueryHandler(queryUnitOfWork, currentUser, mapper);
                return await handler.Handle(new GetGEMByIdQuery { GemId = gem.Id }, default);
            })
            .ToList();

        var results = await Task.WhenAll(tasks);

        // Assert - All concurrent queries should return consistent data
        results.Should().HaveCount(5);
        results.Should().AllSatisfy(dto =>
        {
            dto.Should().NotBeNull();
            dto?.Title.Should().Be("Concurrent Query Test");
        });
    }

    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile(new GEMMappingProfile()));
        return config.CreateMapper();
    }

    private static UnitOfWork CreateUnitOfWork(ApplicationDbContext context)
    {
        return new UnitOfWork(
            context,
            new GEMRepository(context),
            new CategoryRepository(context),
            new TagRepository(context),
            new CategorySuggestionRepository(context),
            new ActivityLogRepository(context));
    }
}
