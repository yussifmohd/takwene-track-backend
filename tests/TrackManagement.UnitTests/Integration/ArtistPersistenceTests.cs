using Microsoft.EntityFrameworkCore;
using Npgsql;
using TrackManagement.Application.Features.Artists.Specifications;
using TrackManagement.Domain.Artists;
using TrackManagement.Domain.Dsps;
using TrackManagement.Infrastructure.Persistence;
using TrackManagement.Infrastructure.Persistence.Configurations;
using TrackManagement.UnitTests.Application.Artists;

namespace TrackManagement.UnitTests.Integration;

/// <summary>Behaviour that only the real database engine can prove (Unicode, unique index, SQL ordering, encoding).</summary>
[Collection(PostgresDatabase.Name)]
[Trait("Category", "Integration")]
public sealed class ArtistPersistenceTests(PostgresFixture postgres)
{
    private static readonly DateTime Now = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.com";

    [Theory]
    [InlineData("عمرو دياب")]
    [InlineData("Nancy عجرم")]
    [InlineData("فيروز ✨")]
    [InlineData("محمد عبده (مُحمّد)")]
    [InlineData("é combining + 🎤🎶")]
    public async Task Names_RoundTripByteForByte(string name)
    {
        // Arrange
        var artist = Artist.Create(name, "EG", UniqueEmail(), Now);
        await using (var write = postgres.CreateContext())
        {
            write.Add(artist);
            await write.SaveChangesAsync(Ct);
        }

        // Act
        await using var read = postgres.CreateContext();
        var stored = await read.Set<Artist>().AsNoTracking().SingleAsync(a => a.Id == artist.Id, Ct);

        // Assert: same UTF-16 units, no normalisation
        stored.Name.ShouldBe(name);
        stored.Name.ToCharArray().ShouldBe(name.ToCharArray());
    }

    [Fact]
    public async Task NameAt200CodePoints_FitsTheColumn()
    {
        var name = string.Concat(Enumerable.Repeat("ع🎤", 100)); // 200 code points, 300 UTF-16 units, 700 bytes
        await using var context = postgres.CreateContext();
        context.Add(Artist.Create(name, "SA", UniqueEmail(), Now));

        await Should.NotThrowAsync(() => context.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task DuplicateNormalizedEmail_IsRejectedByTheUniqueIndex()
    {
        // Arrange
        var email = UniqueEmail();
        await using (var first = postgres.CreateContext())
        {
            first.Add(Artist.Create("One", "EG", email, Now));
            await first.SaveChangesAsync(Ct);
        }

        // Act: a racing request that got past the validator
        await using var second = postgres.CreateContext();
        second.Add(Artist.Create("Two", "EG", "  " + email.ToUpperInvariant() + " ", Now));
        var exception = await Should.ThrowAsync<DbUpdateException>(() => second.SaveChangesAsync(Ct));

        // Assert: the shape UniqueConstraintExceptionHandler maps to 409
        var postgresError = exception.InnerException.ShouldBeOfType<PostgresException>();
        postgresError.SqlState.ShouldBe("23505");
        postgresError.ConstraintName.ShouldBe(ArtistConfiguration.NormalizedEmailIndex);
    }

    [Fact]
    public async Task PageSpec_OrdersByLowerNameInSql_AndOmitsEmailForVisitors()
    {
        // Arrange: a private database so other tests' rows don't interfere
        var connection = await postgres.CreateDatabaseAsync($"order_{Guid.NewGuid():N}", "UTF8");
        await using (var setup = postgres.CreateContext(connectionString: connection))
        {
            await setup.Database.MigrateAsync(Ct);
            setup.AddRange(
                Artist.Create("beta", "EG", UniqueEmail(), Now.AddMinutes(1)),
                Artist.Create("Alpha", "EG", UniqueEmail(), Now.AddMinutes(2)),
                Artist.Create("alpha", "EG", UniqueEmail(), Now.AddMinutes(1)),
                Artist.Create("Zed", "EG", UniqueEmail(), Now));
            await setup.SaveChangesAsync(Ct);
        }

        // Act
        await using var context = postgres.CreateContext(connectionString: connection);
        var repository = new EfRepository<Artist>(context);
        var page = await repository.ListAsync(new ArtistsPageSpec(1, 3, includeEmail: false), Ct);
        var total = await repository.CountAsync(new ArtistsCountSpec(), Ct);

        // Assert
        page.Select(a => a.Name).ShouldBe(["alpha", "Alpha", "beta"]);
        page.ShouldAllBe(a => a.Email == null);
        total.ShouldBe(4);
    }

    [Fact]
    public async Task AuditTimestamps_AreFilledOnInsertAndUpdate()
    {
        // Arrange
        var clock = new FixedTimeProvider(new DateTimeOffset(Now.AddHours(1)));
        var artist = Artist.Create("Audited", "JO", UniqueEmail(), Now);
        await using (var context = postgres.CreateContext(clock))
        {
            context.Add(artist);
            await context.SaveChangesAsync(Ct);

            // Act
            context.Entry(artist).Property(a => a.Name).CurrentValue = "Audited (renamed)";
            await context.SaveChangesAsync(Ct);
        }

        // Assert
        await using var read = postgres.CreateContext();
        var stored = await read.Set<Artist>().AsNoTracking().SingleAsync(a => a.Id == artist.Id, Ct);
        stored.CreatedAtUtc.ShouldBe(Now);
        stored.UpdatedAtUtc.ShouldBe(Now.AddHours(1));
    }

    [Fact]
    public async Task Migration_SeedsExactlyTheThreeDsps()
    {
        await using var context = postgres.CreateContext();

        var dsps = await context.Set<Dsp>().AsNoTracking().OrderBy(d => d.Name).ToListAsync(Ct);

        dsps.Select(d => (d.Id, d.Name)).ShouldBe([
            (Dsp.AppleMusicId, "Apple Music"),
            (Dsp.SpotifyId, "Spotify"),
            (Dsp.YouTubeMusicId, "YouTube Music"),
        ]);
    }

    [Fact]
    public async Task MigratingAgain_DoesNotDuplicateDsps()
    {
        await using var context = postgres.CreateContext();
        await context.Database.MigrateAsync(Ct);

        (await context.Set<Dsp>().CountAsync(Ct)).ShouldBe(3);
    }

    [Fact]
    public async Task EncodingCheck_PassesOnUtf8()
    {
        await using var context = postgres.CreateContext();
        await Should.NotThrowAsync(() => DatabaseEncodingCheck.EnsureUtf8Async(context, Ct));
    }

    [Fact]
    public async Task EncodingCheck_RefusesANonUtf8Database()
    {
        // Arrange
        var connection = await postgres.CreateDatabaseAsync($"ascii_{Guid.NewGuid():N}", "SQL_ASCII");
        await using var context = postgres.CreateContext(connectionString: connection);

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(() => DatabaseEncodingCheck.EnsureUtf8Async(context, Ct));

        // Assert
        exception.Message.ShouldContain("SQL_ASCII");
        exception.Message.ShouldContain("UTF8");
    }
}
