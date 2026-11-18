using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TrackManagement.Domain.Artists;
using TrackManagement.Domain.Tracks;
using TrackManagement.Infrastructure.Identity;
using TrackManagement.Infrastructure.Persistence;
using TrackManagement.Infrastructure.Persistence.Seeding;

namespace TrackManagement.UnitTests.Integration;

/// <summary>
/// The sample goes only into an empty catalogue, never changes existing data or the admin, and two
/// instances starting together add it once. Each test gets its own fresh, migrated database.
/// </summary>
[Collection(PostgresDatabase.Name)]
[Trait("Category", "Integration")]
public sealed class SampleCatalogSeederTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly SampleCatalogSeeder _sut = new(NullLogger<SampleCatalogSeeder>.Instance);

    private async Task<string> FreshDatabaseAsync()
    {
        var connectionString = await postgres.CreateDatabaseAsync($"seed_{Guid.NewGuid():N}", "UTF8");
        await using var context = postgres.CreateContext(connectionString: connectionString);
        await context.Database.MigrateAsync(Ct);
        return connectionString;
    }

    private async Task<(int Artists, int Tracks, int Deliveries)> CountAsync(string connectionString)
    {
        await using var context = postgres.CreateContext(connectionString: connectionString);
        return (await context.Set<Artist>().CountAsync(Ct),
            await context.Set<Track>().CountAsync(Ct),
            await context.Set<Track>().SelectMany(t => t.Distributions).CountAsync(Ct));
    }

    [Fact]
    public async Task EmptyCatalogue_GetsTheWholeSample()
    {
        // Arrange
        var db = await FreshDatabaseAsync();

        // Act
        await using (var context = postgres.CreateContext(connectionString: db))
        {
            await _sut.SeedAsync(context, Ct);
        }

        // Assert
        var expectedDeliveries = SampleCatalog.Tracks.Sum(t => t.Deliveries.Count);
        (await CountAsync(db)).ShouldBe((SampleCatalog.Artists.Count, SampleCatalog.Tracks.Count, expectedDeliveries));

        await using var read = postgres.CreateContext(connectionString: db);
        var stairway = await read.Set<Track>().Include(t => t.Distributions).SingleAsync(t => t.Title == "Stairway to Heaven", Ct);
        stairway.Status.ShouldBe(TrackStatus.Distributed);
        stairway.Distributions.Count.ShouldBe(2);
        (await read.Set<Artist>().AnyAsync(a => a.Name == "محمد عبده", Ct)).ShouldBeTrue("Arabic names are stored as written");
    }

    [Fact]
    public async Task SecondRun_ChangesNothing()
    {
        // Arrange
        var db = await FreshDatabaseAsync();
        await using (var first = postgres.CreateContext(connectionString: db))
        {
            await _sut.SeedAsync(first, Ct);
        }

        var before = await CountAsync(db);

        // Act
        await using (var second = postgres.CreateContext(connectionString: db))
        {
            await _sut.SeedAsync(second, Ct);
        }

        // Assert
        (await CountAsync(db)).ShouldBe(before);
    }

    [Fact]
    public async Task ExistingArtist_SkipsTheSample_AndLeavesTheArtistAlone()
    {
        // Arrange
        var db = await FreshDatabaseAsync();
        var real = Artist.Create("Real Artist", "EG", "real@label.test", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await using (var write = postgres.CreateContext(connectionString: db))
        {
            write.Add(real);
            await write.SaveChangesAsync(Ct);
        }

        // Act
        await using (var context = postgres.CreateContext(connectionString: db))
        {
            await _sut.SeedAsync(context, Ct);
        }

        // Assert
        (await CountAsync(db)).ShouldBe((1, 0, 0));
        await using var read = postgres.CreateContext(connectionString: db);
        var stored = await read.Set<Artist>().AsNoTracking().SingleAsync(Ct);
        (stored.Id, stored.Name, stored.Email, stored.UpdatedAtUtc).ShouldBe((real.Id, real.Name, real.Email, (DateTime?)null));
    }

    [Fact]
    public async Task AdminOnlyDatabase_GetsTheSample_AndTheAdminIsUnchanged()
    {
        // Arrange
        var db = await FreshDatabaseAsync();
        var admin = new ApplicationUser
        {
            UserName = "admin@takwene.test",
            NormalizedUserName = "ADMIN@TAKWENE.TEST",
            Email = "admin@takwene.test",
            NormalizedEmail = "ADMIN@TAKWENE.TEST",
            PasswordHash = "hash-that-must-not-change",
            ConcurrencyStamp = "stamp-that-must-not-change",
        };
        await using (var write = postgres.CreateContext(connectionString: db))
        {
            write.Users.Add(admin);
            await write.SaveChangesAsync(Ct);
        }

        // Act
        await using (var context = postgres.CreateContext(connectionString: db))
        {
            await _sut.SeedAsync(context, Ct);
        }

        // Assert
        (await CountAsync(db)).Artists.ShouldBe(SampleCatalog.Artists.Count);
        await using var read = postgres.CreateContext(connectionString: db);
        var users = await read.Users.AsNoTracking().ToListAsync(Ct);
        users.Count.ShouldBe(1);
        var stored = users[0];
        (stored.Id, stored.Email, stored.PasswordHash, stored.SecurityStamp, stored.ConcurrencyStamp)
            .ShouldBe((admin.Id, admin.Email, admin.PasswordHash, admin.SecurityStamp, admin.ConcurrencyStamp));
    }

    [Fact]
    public async Task TwoInstancesStartingTogether_AddTheSampleOnce()
    {
        // Arrange
        var db = await FreshDatabaseAsync();
        await using var a = postgres.CreateContext(connectionString: db);
        await using var b = postgres.CreateContext(connectionString: db);
        var other = new SampleCatalogSeeder(NullLogger<SampleCatalogSeeder>.Instance);

        // Act – without the lock both would see an empty catalogue and the second save would hit the unique ISRC index.
        await Task.WhenAll(_sut.SeedAsync(a, Ct), other.SeedAsync(b, Ct));

        // Assert
        (await CountAsync(db)).Artists.ShouldBe(SampleCatalog.Artists.Count);
    }
}
