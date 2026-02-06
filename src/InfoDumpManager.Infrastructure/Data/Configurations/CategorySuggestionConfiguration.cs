using InfoDumpManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfoDumpManager.Infrastructure.Data.Configurations;

public sealed class CategorySuggestionConfiguration : IEntityTypeConfiguration<CategorySuggestion>
{
    public void Configure(EntityTypeBuilder<CategorySuggestion> builder)
    {
        builder.ToTable("CategorySuggestions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.GEMId).IsRequired();
        builder.Property(x => x.SuggestedCategoryId).IsRequired(false);
        builder.Property(x => x.ProposedCategoryName).HasMaxLength(128);
        builder.Property(x => x.ConfidenceScore).IsRequired();
        builder.Property(x => x.Rationale).HasMaxLength(2048);
        builder.Property(x => x.AutoAssigned).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired(false);

        builder.HasIndex(x => new { x.TenantId, x.GEMId });
        builder.HasIndex(x => x.SuggestedCategoryId);
    }
}
