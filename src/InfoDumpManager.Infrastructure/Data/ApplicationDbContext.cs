using System;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Infrastructure.Data.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace InfoDumpManager.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public DbSet<GEM> Gems => Set<GEM>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfiguration(new GEMConfiguration());
        builder.ApplyConfiguration(new CategoryConfiguration());
        builder.ApplyConfiguration(new ActivityLogConfiguration());
        builder.ApplyConfiguration(new UserConfiguration());
    }
}
