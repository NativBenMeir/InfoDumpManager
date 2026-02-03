using InfoDumpManager.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfoDumpManager.Infrastructure.Data.Configurations;

public sealed class CostUsageEntryConfiguration : IEntityTypeConfiguration<CostUsageEntry>
{
    public void Configure(EntityTypeBuilder<CostUsageEntry> builder)
    {
        builder.ToTable("cost_usage_entries");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Operation)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.CreatedAt });
    }
}
