using System.Diagnostics.CodeAnalysis;
using InfoDumpManager.Application.Services.Embeddings;
using InfoDumpManager.Infrastructure.Data;
using InfoDumpManager.Infrastructure.Data.Entities;
using InfoDumpManager.Infrastructure.Services.Embeddings;
using InfoDumpManager.Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace InfoDumpManager.Tests.Integration.AIAgents;

[ExcludeFromCodeCoverage]
[Collection("IntegrationTests")]
public sealed class PostgreSqlVectorStoreIntegrationTests : IAsyncLifetime
{
    private const int VectorSize = 1536;
    private readonly PostgresTestcontainerFixture _fixture;
    private ApplicationDbContext _dbContext = null!;
    private PostgreSqlVectorStore _vectorStore = null!;

    public PostgreSqlVectorStoreIntegrationTests(PostgresTestcontainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _dbContext = _fixture.CreateContext();
        var connection = (NpgsqlConnection)_dbContext.Database.GetDbConnection();
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
        await _dbContext.Database.MigrateAsync();
        await _dbContext.Database.ExecuteSqlRawAsync(@"
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
        
        _vectorStore = new PostgreSqlVectorStore(_dbContext);
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task StoreAsync_ShouldPersistEmbeddingToDatabase()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var vector = BuildVector(0.1f, 0.2f, 0.3f, 0.4f);
        var metadata = "{\"type\":\"test\",\"name\":\"sample\"}";

        var record = new EmbeddingRecord(
            Guid.NewGuid(),
            tenantId,
            sourceId,
            "test-source",
            "text-embedding-3-large",
            vector,
            metadata,
            DateTimeOffset.UtcNow);

        // Act
        await _vectorStore.StoreAsync(record);
        await _dbContext.SaveChangesAsync();

        // Assert
        var stored = await _dbContext.EmbeddingRecords
            .FirstOrDefaultAsync(e => e.SourceId == sourceId);

        Assert.NotNull(stored);
        Assert.Equal(tenantId, stored.TenantId);
        Assert.Equal("test-source", stored.ContentType);
        Assert.Equal(metadata, stored.MetadataJson);
    }

    [Fact]
    public async Task SearchAsync_ShouldReturnResultsOrderedBySimilarity()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var queryVector = BuildVector(0.5f, 0.5f, 0.5f);

        // Store similar and dissimilar vectors (both with same content type for search)
        var similar = new EmbeddingRecord(
            Guid.NewGuid(),
            tenantId,
            Guid.NewGuid(),
            "test",
            "text-embedding-3-large",
            BuildVector(0.4f, 0.6f, 0.5f), // Close to query
            "{\"name\":\"similar\"}",
            DateTimeOffset.UtcNow);

        var dissimilar = new EmbeddingRecord(
            Guid.NewGuid(),
            tenantId,
            Guid.NewGuid(),
            "test",
            "text-embedding-3-large",
            BuildVector(0.9f, 0.1f, 0.0f), // Far from query
            "{\"name\":\"dissimilar\"}",
            DateTimeOffset.UtcNow);

        await _vectorStore.StoreAsync(similar);
        await _vectorStore.StoreAsync(dissimilar);
        await _dbContext.SaveChangesAsync();

        var request = new EmbeddingSearchRequest(
            tenantId,
            "test",
            queryVector,
            2);

        // Act
        var results = await _vectorStore.SearchSimilarAsync(request);

        // Assert
        Assert.NotEmpty(results);
        var resultsList = results.ToList();
        
        // First result should be more similar (lower distance)
        Assert.True(resultsList[0].Distance < resultsList[^1].Distance);
    }

    [Fact]
    public async Task SearchAsync_ShouldFilterBySourceType()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var vector = BuildVector(0.1f, 0.2f);

        await _vectorStore.StoreAsync(new EmbeddingRecord(
            Guid.NewGuid(), tenantId, Guid.NewGuid(), "category", "text-embedding-3-large", vector, "{}", DateTimeOffset.UtcNow));

        await _vectorStore.StoreAsync(new EmbeddingRecord(
            Guid.NewGuid(), tenantId, Guid.NewGuid(), "tag", "text-embedding-3-large", vector, "{}", DateTimeOffset.UtcNow));

        await _dbContext.SaveChangesAsync();

        var request = new EmbeddingSearchRequest(tenantId, "category", vector, 10);

        // Act
        var results = await _vectorStore.SearchSimilarAsync(request);

        // Assert
        Assert.All(results, r =>
        {
            var record = _dbContext.EmbeddingRecords.FirstOrDefault(x => x.SourceId == r.SourceId);
            Assert.Equal("category", record?.ContentType);
        });
    }

    [Fact]
    public async Task SearchAsync_ShouldFilterByTenant()
    {
        // Arrange
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();
        var vector = BuildVector(0.3f, 0.7f);

        await _vectorStore.StoreAsync(new EmbeddingRecord(
            Guid.NewGuid(), tenant1, Guid.NewGuid(), "test", "text-embedding-3-large", vector, "{}", DateTimeOffset.UtcNow));

        await _vectorStore.StoreAsync(new EmbeddingRecord(
            Guid.NewGuid(), tenant2, Guid.NewGuid(), "test", "text-embedding-3-large", vector, "{}", DateTimeOffset.UtcNow));

        await _dbContext.SaveChangesAsync();

        var request = new EmbeddingSearchRequest(tenant1, "test", vector, 10);

        // Act
        var results = await _vectorStore.SearchSimilarAsync(request);

        // Assert
        Assert.All(results, r =>
        {
            var record = _dbContext.EmbeddingRecords.FirstOrDefault(x => x.SourceId == r.SourceId);
            Assert.Equal(tenant1, record?.TenantId);
        });
    }

    [Fact]
    public async Task SearchAsync_WithEmptyVector_ShouldThrowArgumentException()
    {
        // Arrange
        var emptyVector = Array.Empty<float>();
        var request = new EmbeddingSearchRequest(
            Guid.NewGuid(),
            "test",
            emptyVector,
            5);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _vectorStore.SearchSimilarAsync(request));
    }

    [Fact]
    public async Task StoreAndSearch_WithConcurrentWrites_ShouldHandleCorrectly()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tasks = Enumerable.Range(0, 10)
            .Select(i => new EmbeddingRecord(
                Guid.NewGuid(),
                tenantId,
                Guid.NewGuid(),
                "concurrent",
                "text-embedding-3-large",
                BuildVector(i * 0.1f, i * 0.2f),
                $"{{\"index\":{i}}}",
                DateTimeOffset.UtcNow))
            .Select(record => _vectorStore.StoreAsync(record))
            .ToList();

        // Act
        await Task.WhenAll(tasks);
        await _dbContext.SaveChangesAsync();

        // Assert
        var count = await _dbContext.EmbeddingRecords
            .CountAsync(e => e.TenantId == tenantId && e.ContentType == "concurrent");

        Assert.Equal(10, count);
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
}
