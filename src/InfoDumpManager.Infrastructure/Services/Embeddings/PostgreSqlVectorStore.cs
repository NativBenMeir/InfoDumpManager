using InfoDumpManager.Application.Services.Embeddings;
using InfoDumpManager.Infrastructure.Data;
using InfoDumpManager.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector;

namespace InfoDumpManager.Infrastructure.Services.Embeddings;

public sealed class PostgreSqlVectorStore : IVectorStore
{
    private static readonly SemaphoreSlim DbSemaphore = new(1, 1);
    private readonly ApplicationDbContext _context;

    public PostgreSqlVectorStore(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task StoreAsync(EmbeddingRecord record, CancellationToken cancellationToken = default)
    {
        if (record is null)
        {
            throw new ArgumentNullException(nameof(record));
        }

        await DbSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entity = new EmbeddingRecordEntity
            {
                Id = record.Id,
                TenantId = record.TenantId,
                SourceId = record.SourceId,
                ContentType = record.ContentType,
                Model = record.Model,
                Vector = new Vector(record.Vector),
                MetadataJson = record.Metadata,
                CreatedAt = record.CreatedAt
            };

            await _context.EmbeddingRecords.AddAsync(entity, cancellationToken).ConfigureAwait(false);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            DbSemaphore.Release();
        }
    }

    public async Task<IReadOnlyList<EmbeddingSearchResult>> SearchSimilarAsync(
        EmbeddingSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.QueryVector.Length == 0)
        {
            throw new ArgumentException("Query vector cannot be empty.", nameof(request));
        }

        await DbSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        var openedConnection = false;

        var results = new List<EmbeddingSearchResult>();
        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            openedConnection = true;
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT "SourceId", "MetadataJson", ("Vector" <-> @query) AS distance
                FROM embedding_records
                WHERE "TenantId" = @tenant AND "ContentType" = @content_type
                ORDER BY "Vector" <-> @query
                LIMIT @limit;
                """;

            var queryParameter = new NpgsqlParameter("query", new Vector(request.QueryVector))
            {
                DataTypeName = "vector"
            };

            command.Parameters.Add(queryParameter);
            command.Parameters.Add(new NpgsqlParameter("tenant", request.TenantId));
            command.Parameters.Add(new NpgsqlParameter("content_type", request.ContentType));
            command.Parameters.Add(new NpgsqlParameter("limit", request.Limit));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var sourceId = reader.GetGuid(0);
                var metadata = reader.IsDBNull(1) ? null : reader.GetString(1);
                var distance = reader.GetDouble(2);

                results.Add(new EmbeddingSearchResult(sourceId, distance, metadata));
            }
        }
        finally
        {
            if (openedConnection)
            {
                await _context.Database.CloseConnectionAsync().ConfigureAwait(false);
            }

            DbSemaphore.Release();
        }

        return results;
    }
}
