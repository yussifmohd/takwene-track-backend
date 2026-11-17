using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TrackManagement.Infrastructure.Persistence;

/// <summary>
/// Applies migrations and seeders at startup when enabled. Any failure is logged and rethrown so
/// the process exits non-zero instead of serving requests against a broken schema.
/// </summary>
public sealed partial class DatabaseInitializer(
    AppDbContext dbContext,
    IEnumerable<ISeeder> seeders,
    IOptions<DatabaseOptions> options,
    ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Value.MigrateOnStartup)
        {
            LogSkipped(logger);
            return;
        }

        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }

        foreach (var seeder in seeders.OrderBy(s => s.Order))
        {
            await seeder.SeedAsync(dbContext, cancellationToken);
        }

        LogDone(logger);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Database initialisation skipped (Database:MigrateOnStartup=false)")]
    private static partial void LogSkipped(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Database migrated and seeded")]
    private static partial void LogDone(ILogger logger);
}

public static class DatabaseInitializerExtensions
{
    public static async Task InitializeDatabaseAsync(this IHost host, CancellationToken cancellationToken = default)
    {
        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(cancellationToken);
    }
}
