using InfoDumpManager.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfoDumpManager.Infrastructure.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.Property(u => u.TenantId).IsRequired();
        builder.Property(u => u.DisplayName).IsRequired().HasMaxLength(128);
        builder.Property(u => u.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.LastSeenAt).IsRequired(false);
        builder.Property(u => u.RowVersion).IsRowVersion();

        builder.Property(u => u.NormalizedEmail).HasMaxLength(256);
        builder.Property(u => u.NormalizedUserName).HasMaxLength(256);

        builder.HasIndex(u => new { u.TenantId, u.UserName }).IsUnique();
        builder.HasIndex(u => new { u.TenantId, u.Email }).IsUnique();
    }
}
