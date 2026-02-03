using System;
using System.IO;
using InfoDumpManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Pgvector.EntityFrameworkCore;

namespace InfoDumpManager.Infrastructure.Data;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var basePath = ResolveWebApiPath();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = Environment.GetEnvironmentVariable("CONNECTION_STR")
                               ?? "Host=localhost;Database=InfoDumpManager;Username=postgres;Password=postgres;Pooling=false";
        }

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connectionString, options => {
            options.EnableRetryOnFailure();
            options.UseVector();
        });

        return new ApplicationDbContext(optionsBuilder.Options);
    }

    private static string ResolveWebApiPath()
    {
        var directory = Directory.GetCurrentDirectory();

        while (!string.IsNullOrEmpty(directory))
        {
            if (string.Equals(Path.GetFileName(directory), "InfoDumpManager.WebAPI", StringComparison.OrdinalIgnoreCase))
            {
                return directory;
            }

            var candidate = Path.Combine(directory, "src", "InfoDumpManager.WebAPI");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            var parent = Path.GetDirectoryName(directory);
            if (string.IsNullOrEmpty(parent) || parent == directory)
            {
                break;
            }

            directory = parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
