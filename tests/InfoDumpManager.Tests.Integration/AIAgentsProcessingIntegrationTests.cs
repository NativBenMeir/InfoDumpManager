using System.Diagnostics.CodeAnalysis;
using InfoDumpManager.Application.Agents;
using InfoDumpManager.Application.Agents.Orchestration;
using InfoDumpManager.Application.Infrastructure.JobQueue;
using InfoDumpManager.Infrastructure.Services;
using InfoDumpManager.Application.Services.Embeddings;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Domain.ValueObjects;
using InfoDumpManager.Infrastructure.Data;
using InfoDumpManager.Infrastructure.Data.Entities;
using InfoDumpManager.Infrastructure.Repositories;
using InfoDumpManager.Infrastructure.Services.Embeddings;
using InfoDumpManager.Tests.Integration.Fixtures;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Xunit;

namespace InfoDumpManager.Tests.Integration;

[ExcludeFromCodeCoverage]
[Collection("IntegrationTests")]
public sealed class AIAgentsProcessingIntegrationTests : IAsyncLifetime
{
    private const int VectorSize = 1536;
    private readonly PostgresTestcontainerFixture _fixture;
    private ApplicationDbContext _dbContext = null!;

    public AIAgentsProcessingIntegrationTests(PostgresTestcontainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _dbContext = _fixture.CreateContext();
        await _dbContext.Database.EnsureCreatedAsync();
        await EnsureVectorTableAsync(_dbContext);
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task BackgroundQueue_ShouldDrainAndProcessJobs()
    {
        // Arrange
        var orchestrator = new Mock<IContentProcessingOrchestrator>();
        orchestrator
            .Setup(x => x.ProcessGEMAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<ProcessingOptions>(),
                It.IsAny<Guid?>()))
            .ReturnsAsync(new ProcessingResult(
                Guid.NewGuid(),
                ProcessingStatus.Completed,
                null,
                null,
                null,
                null,
                null,
                new List<string>(),
                DateTimeOffset.UtcNow));

        var jobQueue = new InMemoryJobQueue<ProcessingJob>(
            Mock.Of<ILogger<InMemoryJobQueue<ProcessingJob>>>());
        var service = new ContentProcessingBackgroundService(
            jobQueue,
            orchestrator.Object,
            Mock.Of<ILogger<ContentProcessingBackgroundService>>());

        var job = new ProcessingJob(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Content",
            new ProcessingOptions(),
            0,
            DateTimeOffset.UtcNow,
            null);

        // Act
        await jobQueue.EnqueueAsync(job);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.StartAsync(cts.Token);
        await Task.Delay(1000);
        await service.StopAsync(CancellationToken.None);

        // Assert
        orchestrator.Verify(
            x => x.ProcessGEMAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<ProcessingOptions>(),
                It.IsAny<Guid?>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Orchestrator_ShouldPersistSummaryToDatabase()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var source = new GEMSource("https://example.com/source", "Source");
        var snapshot = new GEMSnapshot("Original content", "text/plain", DateTimeOffset.UtcNow);
        var gem = GEM.Create(tenantId, "Test GEM", "https://example.com/gem", source, snapshot);

        await using (var arrangeContext = _fixture.CreateContext())
        {
            arrangeContext.Gems.Add(gem);
            await arrangeContext.SaveChangesAsync();
        }

        var services = new ServiceCollection();
        services.AddScoped<ApplicationDbContext>(_ => _fixture.CreateContext());
        services.AddScoped<IGEMRepository, GEMRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<ICategorySuggestionRepository, CategorySuggestionRepository>();
        services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(x => x.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        services.AddScoped<IMediator>(_ => mediator.Object);
        services.AddScoped<IAgent>(_ => new TestAgent(AgentCapability.Summarization, "Summary", payload =>
        {
            payload["summaryObject"] = GEMSummary.Create("AI summary", "test-model", 10, DateTimeOffset.UtcNow);
        }));
        services.AddScoped<IAgent>(_ => new TestAgent(AgentCapability.Categorization, "Categorization"));
        services.AddScoped<IAgent>(_ => new TestAgent(AgentCapability.Tagging, "Tagging"));
        services.AddScoped<IAgent>(_ => new TestAgent(AgentCapability.Validation, "Validation"));

        var provider = services.BuildServiceProvider();
        var jobTracker = new InMemoryJobTracker();
        var orchestrator = new ContentProcessingOrchestrator(
            provider.GetRequiredService<IServiceScopeFactory>(),
            jobTracker,
            Mock.Of<ILogger<ContentProcessingOrchestrator>>());

        // Act
        var result = await orchestrator.ProcessGEMAsync(
            gem.Id,
            tenantId,
            gem.Snapshot.HtmlContent,
            new ProcessingOptions(RunValidation: true));

        // Assert
        Assert.Equal(ProcessingStatus.Completed, result.Status);

        await using (var verifyContext = _fixture.CreateContext())
        {
            var refreshed = await verifyContext.Gems.FirstAsync(g => g.Id == gem.Id);
            Assert.Equal("AI summary", refreshed.Summary.Text);
        }
    }

    [Fact]
    public async Task VectorStore_ShouldPersistAndSearchEmbeddings()
    {
        // Arrange
        var vectorStore = new PostgreSqlVectorStore(_dbContext);
        var tenantId = Guid.NewGuid();
        var record = new EmbeddingRecord(
            Guid.NewGuid(),
            tenantId,
            Guid.NewGuid(),
            "gem",
            "text-embedding-3-large",
            BuildVector(0.1f, 0.2f, 0.3f),
            "{}",
            DateTimeOffset.UtcNow);

        // Act
        await vectorStore.StoreAsync(record);
        await _dbContext.SaveChangesAsync();

        var results = await vectorStore.SearchSimilarAsync(new EmbeddingSearchRequest(
            tenantId,
            "gem",
            BuildVector(0.1f, 0.2f, 0.3f),
            1));

        // Assert
        Assert.Single(results);
    }

    private static async Task EnsureVectorTableAsync(ApplicationDbContext dbContext)
    {
        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "CREATE EXTENSION IF NOT EXISTS vector";
            await command.ExecuteNonQueryAsync();
        }

        await connection.ReloadTypesAsync();
        await connection.CloseAsync();

        await dbContext.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS embedding_records (
                ""Id"" uuid PRIMARY KEY,
                ""TenantId"" uuid NOT NULL,
                ""SourceId"" uuid NOT NULL,
                ""ContentType"" character varying(64) NOT NULL,
                ""Model"" character varying(128) NOT NULL,
                ""Vector"" vector(1536) NOT NULL,
                ""MetadataJson"" jsonb NULL,
                ""CreatedAt"" timestamp with time zone NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ""IX_embedding_records_TenantId_ContentType""
                ON embedding_records (""TenantId"", ""ContentType"");
        ");
    }

    private static float[] BuildVector(params float[] values)
    {
        var vector = new float[VectorSize];
        var length = Math.Min(values.Length, VectorSize);
        if (length > 0)
        {
            Array.Copy(values, vector, length);
        }

        return vector;
    }

    private sealed class TestAgent : IAgent
    {
        private readonly Action<Dictionary<string, object>>? _configurePayload;

        public TestAgent(AgentCapability capability, string name, Action<Dictionary<string, object>>? configurePayload = null)
        {
            Capability = capability;
            Name = name;
            _configurePayload = configurePayload;
        }

        public string Name { get; }

        public AgentCapability Capability { get; }

        public Task<AgentResult> ExecuteAsync(AgentContext context)
        {
            var payload = new Dictionary<string, object>();
            _configurePayload?.Invoke(payload);

            var result = new AgentResult(
                true,
                $"{Name} completed",
                new AgentResultData(Name, DateTimeOffset.UtcNow, payload),
                new AgentMetrics(10, 0.001m, TimeSpan.FromMilliseconds(10), 0, "test"));

            return Task.FromResult(result);
        }
    }
}
