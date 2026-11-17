namespace TrackManagement.Infrastructure.Persistence;

/// <summary>Idempotent data seeder run after migrations when <see cref="DatabaseOptions.MigrateOnStartup"/> is on.</summary>
public interface ISeeder
{
    /// <summary>Lower runs first.</summary>
    int Order { get; }

    Task SeedAsync(AppDbContext dbContext, CancellationToken cancellationToken);
}
