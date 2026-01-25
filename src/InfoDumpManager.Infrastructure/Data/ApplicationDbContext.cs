using System;
using System.Collections.Generic;
using System.Linq;
using InfoDumpManager.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace InfoDumpManager.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<IdentityUser<Guid>, IdentityRole<Guid>, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<GEM> Gems => Set<GEM>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        var guidListConverter = new ValueConverter<List<Guid>, string>(
            list => string.Join(',', list),
            value => string.IsNullOrWhiteSpace(value)
                ? new List<Guid>()
                : value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToList());

        builder.Entity<GEM>(entity =>
        {
            entity.ToTable("gems");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).IsRequired().HasMaxLength(500);
            entity.OwnsOne(x => x.Source, nav =>
            {
                nav.Property(p => p.Url).HasColumnName("source_url").IsRequired();
            });
            entity.OwnsOne(x => x.Snapshot, nav =>
            {
                nav.Property(p => p.Content).HasColumnName("snapshot_content");
                nav.Property(p => p.ContentType).HasColumnName("snapshot_content_type").HasMaxLength(256);
                nav.Property(p => p.RetrievedAtUtc).HasColumnName("snapshot_retrieved_at_utc");
            });
            entity.OwnsOne(x => x.Summary, nav =>
            {
                nav.Property(p => p.Text).HasColumnName("summary_text");
                nav.Property(p => p.GeneratedAtUtc).HasColumnName("summary_generated_at_utc");
            });
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
            entity.Property<int>("RowVersion").IsConcurrencyToken();
            entity.Ignore(x => x.CategoryIds);
            entity.Property<string>("_categoryIdsJson")
                .HasColumnName("category_ids")
                .HasDefaultValue(string.Empty);
        });

        builder.Entity<Category>(entity =>
        {
            entity.ToTable("categories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
            entity.Ignore(x => x.GemIds);
            entity.Property<string>("_gemIdsJson")
                .HasColumnName("gem_ids")
                .HasDefaultValue(string.Empty);
        });

        builder.Entity<ActivityLog>(entity =>
        {
            entity.ToTable("activity_logs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ActivityType).IsRequired();
            entity.Property(x => x.Message).IsRequired().HasMaxLength(1000);
            entity.Property(x => x.GemId);
            entity.Property(x => x.CategoryId);
            entity.Property(x => x.UserId);
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        });
    }
}
