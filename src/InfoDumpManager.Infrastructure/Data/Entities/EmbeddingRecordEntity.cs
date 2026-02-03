using Pgvector;

namespace InfoDumpManager.Infrastructure.Data.Entities;

public sealed class EmbeddingRecordEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid SourceId { get; set; }

    public string ContentType { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public Vector Vector { get; set; } = default!;

    public string? MetadataJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
