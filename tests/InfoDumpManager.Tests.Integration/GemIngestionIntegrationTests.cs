using System;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using InfoDumpManager.Application.Common.Services;
using InfoDumpManager.Application.GEMs.Commands;
using InfoDumpManager.Application.Infrastructure.JobQueue;
using InfoDumpManager.Application.Mappings;
using InfoDumpManager.Infrastructure.Repositories;
using InfoDumpManager.Infrastructure.Services;
using InfoDumpManager.Tests.Integration.Fixtures;
using InfoDumpManager.Tests.Integration.TestUtilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace InfoDumpManager.Tests.Integration;

[Collection("IntegrationTests")]
public sealed class GemIngestionIntegrationTests
{
    private readonly PostgresTestcontainerFixture _postgresFixture;
    private readonly MinioTestcontainerFixture _minioFixture;

    public GemIngestionIntegrationTests(PostgresTestcontainerFixture postgresFixture, MinioTestcontainerFixture minioFixture)
    {
        _postgresFixture = postgresFixture;
        _minioFixture = minioFixture;
    }

    [Fact]
    public async Task GemCreation_EndToEndUrlToStorage_CreatesGemWithSnapshotReference()
    {
        await using var server = await MockWebServer.StartAsync(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync("<html><head><title>EndToEnd</title></head><body><div>Content</div></body></html>");
        });

        var scrapingOptions = Options.Create(new WebScrapingOptions
        {
            TimeoutSeconds = 2,
            RetryCount = 0
        });

        var scraper = new WebScrapingService(scrapingOptions, NullLogger<WebScrapingService>.Instance);
        var scrapeResult = await scraper.ScrapeAsync(server.BaseUrl);

        var storageService = CreateStorageService();
        var snapshotKey = $"snapshots/{Guid.NewGuid():N}.html";
        var storedKey = await storageService.UploadSnapshotAsync(snapshotKey, scrapeResult.HtmlContent, scrapeResult.MimeType);

        await using var context = _postgresFixture.CreateContext();
        await using var unitOfWork = new UnitOfWork(context);

        var mapper = CreateMapper();
        var currentUser = new TestCurrentUserContext();
        var databasePolicy = new NoOpDatabasePolicy();
        var jobQueue = new InMemoryJobQueue<ProcessingJob>(NullLogger<InMemoryJobQueue<ProcessingJob>>.Instance);
        var handler = new CreateGEMCommandHandler(unitOfWork, currentUser, mapper, databasePolicy, jobQueue);

        var command = new CreateGEMCommand
        {
            Title = "End-to-End GEM",
            Url = scrapeResult.Url,
            SourceUrl = scrapeResult.Url,
            SourceTitle = scrapeResult.Title,
            SnapshotHtml = scrapeResult.HtmlContent,
            SnapshotMimeType = scrapeResult.MimeType,
            SnapshotCapturedAt = scrapeResult.CapturedAt,
            SummaryText = string.Empty,
            SummaryModel = string.Empty
        };

        var created = await handler.Handle(command, default);

        created.Should().NotBeNull();
        created.Url.Should().Be(scrapeResult.Url);

        var retrieved = await storageService.GetSnapshotAsync(storedKey);
        retrieved.Should().Be(scrapeResult.HtmlContent);
    }

    private MinioStorageService CreateStorageService()
    {
        var options = Options.Create(new MinioOptions
        {
            Endpoint = _minioFixture.Endpoint,
            AccessKey = _minioFixture.UserName,
            SecretKey = _minioFixture.Password,
            BucketName = _minioFixture.BucketName,
            UseSsl = false
        });

        return new MinioStorageService(options, NullLogger<MinioStorageService>.Instance);
    }

    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile(new GEMMappingProfile()));
        return config.CreateMapper();
    }

    private sealed class TestCurrentUserContext : ICurrentUserContext
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid TenantId { get; } = Guid.NewGuid();
        public bool IsAuthenticated => true;
    }

    private sealed class NoOpDatabasePolicy : IDatabasePolicy
    {
        public Task ExecuteAsync(Func<Task> action, System.Threading.CancellationToken cancellationToken = default)
        {
            return action();
        }

        public Task<T> ExecuteAsync<T>(Func<Task<T>> action, System.Threading.CancellationToken cancellationToken = default)
        {
            return action();
        }
    }
}
