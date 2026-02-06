using System;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Infrastructure.Data.Configurations;
using InfoDumpManager.Infrastructure.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace InfoDumpManager.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{

    public DbSet<GEM> Gems => Set<GEM>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<CategorySuggestion> CategorySuggestions => Set<CategorySuggestion>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<EmbeddingRecordEntity> EmbeddingRecords => Set<EmbeddingRecordEntity>();
    public DbSet<CostUsageEntry> CostUsageEntries => Set<CostUsageEntry>();

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasPostgresExtension("vector");

        builder.ApplyConfiguration(new GEMConfiguration());
        builder.ApplyConfiguration(new CategoryConfiguration());
        builder.ApplyConfiguration(new TagConfiguration());
        builder.ApplyConfiguration(new CategorySuggestionConfiguration());
        builder.ApplyConfiguration(new ActivityLogConfiguration());
        builder.ApplyConfiguration(new UserConfiguration());
        builder.ApplyConfiguration(new EmbeddingRecordConfiguration());
        builder.ApplyConfiguration(new CostUsageEntryConfiguration());
    }
}
