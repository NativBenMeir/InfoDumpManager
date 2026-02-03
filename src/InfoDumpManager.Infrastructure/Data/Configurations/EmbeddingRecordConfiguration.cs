using InfoDumpManager.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfoDumpManager.Infrastructure.Data.Configurations;

public sealed class EmbeddingRecordConfiguration : IEntityTypeConfiguration<EmbeddingRecordEntity>
{
    public void Configure(EntityTypeBuilder<EmbeddingRecordEntity> builder)
    {
        builder.ToTable("embedding_records");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ContentType)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Model)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.Vector)
            .HasColumnType("vector(1536)")
            .IsRequired();

        builder.Property(x => x.MetadataJson)
            .HasColumnType("jsonb");

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.ContentType });
    }
}
