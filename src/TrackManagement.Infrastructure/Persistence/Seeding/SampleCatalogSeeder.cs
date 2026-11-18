using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrackManagement.Domain.Artists;
using TrackManagement.Domain.Tracks;

namespace TrackManagement.Infrastructure.Persistence.Seeding;

/// <summary>
/// Adds <see cref="SampleCatalog"/> only into an empty catalogue (no artists and no tracks), so it can
/// never mix with or change real data, and every later start is a no-op. Runs after the admin seeder and
/// never touches accounts. On PostgreSQL a transaction-level advisory lock makes a second instance that
/// starts at the same moment wait and then find the catalogue filled.
/// </summary>
public sealed partial class SampleCatalogSeeder(ILogger<SampleCatalogSeeder> logger) : ISeeder
{
    /// <summary>Constant key for <c>pg_advisory_xact_lock</c> (no user input reaches this SQL).</summary>
    public const long AdvisoryLockKey = 0x5345_4544_0006; // "SEED" + phase

    public int Order => 20;

    public async Task SeedAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            await SeedIfEmptyAsync(dbContext, cancellationToken);
            return;
        }

        // The connection retries on transient failures, so the transaction runs inside the strategy.
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async ct =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            await dbContext.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({AdvisoryLockKey})", ct);
            if (await SeedIfEmptyAsync(dbContext, ct))
            {
                await transaction.CommitAsync(ct);
            }
        }, cancellationToken);
    }

    private async Task<bool> SeedIfEmptyAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        if (await dbContext.Set<Artist>().AnyAsync(cancellationToken) || await dbContext.Set<Track>().AnyAsync(cancellationToken))
        {
            LogSkipped(logger);
            return false;
        }

        var sample = SampleCatalog.Build();
        dbContext.Set<Artist>().AddRange(sample.Artists);
        dbContext.Set<Track>().AddRange(sample.Tracks);
        await dbContext.SaveChangesAsync(cancellationToken);
        LogAdded(logger, sample.Artists.Count, sample.Tracks.Count);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sample catalogue skipped: the catalogue already has data")]
    private static partial void LogSkipped(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sample catalogue added: {Artists} artists and {Tracks} tracks (all sample data)")]
    private static partial void LogAdded(ILogger logger, int artists, int tracks);
}
