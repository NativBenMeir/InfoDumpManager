using System.Text.Json;
using InfoDumpManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfoDumpManager.Infrastructure.Data.Configurations;

public sealed class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> builder)
    {
        builder.ToTable("ActivityLogs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.EventType).IsRequired().HasConversion<string>();
        builder.Property(x => x.EntityName).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(1024);
        builder.Property(x => x.OccurredAt).IsRequired();

        builder.Property(x => x.Metadata)
            .HasColumnType("jsonb")
            .HasConversion(
                metadata => metadata == null ? null : metadata.RootElement.GetRawText(),
                json => string.IsNullOrEmpty(json) ? null : JsonDocument.Parse(json, new JsonDocumentOptions()));

        builder.HasIndex(x => new { x.TenantId, x.EventType });
    }
}
