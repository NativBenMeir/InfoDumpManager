using InfoDumpManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace InfoDumpManager.Infrastructure.Data.Configurations;

public sealed class GEMConfiguration : IEntityTypeConfiguration<GEM>
{
    public void Configure(EntityTypeBuilder<GEM> builder)
    {
        builder.ToTable("Gems");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Title).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Url).IsRequired().HasMaxLength(2048);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired(false);
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);

        builder.HasIndex(x => new { x.TenantId, x.Title });
        builder.HasIndex(x => x.CategoryId);

        builder.HasOne(x => x.Category)
            .WithMany(x => x.Gems)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsOne(x => x.Source, source =>
        {
            source.Property(x => x.Url).HasColumnName("SourceUrl").IsRequired().HasMaxLength(2048);
            source.Property(x => x.Title).HasColumnName("SourceTitle").HasMaxLength(256);
        });

        builder.OwnsOne(x => x.Snapshot, snapshot =>
        {
            snapshot.Property(x => x.HtmlContent).HasColumnName("SnapshotHtml").HasColumnType("text").IsRequired();
            snapshot.Property(x => x.TextContent).HasColumnName("SnapshotText").HasColumnType("text").IsRequired(false);
            snapshot.Property(x => x.MimeType).HasColumnName("SnapshotMimeType").HasMaxLength(64).IsRequired();
            snapshot.Property(x => x.CapturedAt).HasColumnName("SnapshotCapturedAt").IsRequired();
        });

        builder.OwnsOne(x => x.Summary, summary =>
        {
            summary.Property(x => x.Text).HasColumnName("SummaryText").HasColumnType("text").IsRequired(false);
            summary.Property(x => x.Model).HasColumnName("SummaryModel").HasMaxLength(128).IsRequired(false);
            summary.Property(x => x.TokenCount).HasColumnName("SummaryTokenCount");
            summary.Property(x => x.GeneratedAt).HasColumnName("SummaryGeneratedAt");
        });

        var floatArrayToVectorConverter = new ValueConverter<float[]?, Vector?>(
            v => v == null ? null : new Vector(v),
            v => v == null ? null : v.ToArray()
        );

        builder.Property(x => x.TitleEmbedding)
            .HasColumnType("vector(1536)")
            .IsRequired(false)
            .HasConversion(floatArrayToVectorConverter);

        builder.Property(x => x.SummaryEmbedding)
            .HasColumnType("vector(1536)")
            .IsRequired(false)
            .HasConversion(floatArrayToVectorConverter);

        builder.HasIndex(x => x.TitleEmbedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops");

        builder.HasIndex(x => x.SummaryEmbedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops");
    }
}
