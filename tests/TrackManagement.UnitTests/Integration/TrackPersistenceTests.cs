using Microsoft.EntityFrameworkCore;
using Npgsql;
using TrackManagement.Application.Features.Tracks.Specifications;
using TrackManagement.Domain.Artists;
using TrackManagement.Domain.Tracks;
using TrackManagement.Infrastructure.Persistence;
using TrackManagement.Infrastructure.Persistence.Configurations;

namespace TrackManagement.UnitTests.Integration;

/// <summary>Track behaviour only the real engine can prove: Unicode, checks, FK, unique ISRC, SQL filters/ordering/projection.</summary>
[Collection(PostgresDatabase.Name)]
[Trait("Category", "Integration")]
public sealed class TrackPersistenceTests(PostgresFixture postgres)
{
    private static readonly DateTime Now = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A unique, valid ISRC per call (registrant from a counter, so parallel tests never clash).</summary>
    private static int _counter;

    internal static string NewIsrc() => $"ZZ{Interlocked.Increment(ref _counter) % 1000:000}{Random.Shared.Next(0, 9_999_999):0000000}";

    private async Task<Artist> NewArtistAsync(string name = "Amr Diab")
    {
        var artist = Artist.Create(name, "EG", $"{Guid.NewGuid():N}@example.com", Now);
        await using var context = postgres.CreateContext();
        context.Add(artist);
        await context.SaveChangesAsync(Ct);
        return artist;
    }

    private static Track NewTrack(Artist artist, string title = "Track", string genre = "Rap", int year = 2026, string? isrc = null) =>
        Track.Create(title, artist.Id, isrc ?? NewIsrc(), genre, new DateOnly(year, 1, 1), Now);

    [Theory]
    [InlineData("تملي معاك")]
    [InlineData("Ya Tabtab يا طبطب ✨")]
    [InlineData("مُحمّد (Remix) 🎶")]
    public async Task Titles_RoundTripUnchanged(string title)
    {
        // Arrange
        var artist = await NewArtistAsync();
        var track = NewTrack(artist, title);
        await using (var write = postgres.CreateContext())
        {
            write.Add(track);
            await write.SaveChangesAsync(Ct);
        }

        // Act
        await using var read = postgres.CreateContext();
        var stored = await read.Set<Track>().AsNoTracking().SingleAsync(t => t.Id == track.Id, Ct);

        // Assert
        stored.Title.ToCharArray().ShouldBe(title.ToCharArray());
        stored.Status.ShouldBe(TrackStatus.Draft);
        stored.ReleaseDate.ShouldBe(new DateOnly(2026, 1, 1));
        stored.Version.ShouldNotBe(0u);
    }

    [Theory]
    [InlineData("UPDATE tracks SET genre = 'Pop' WHERE id = @id", "ck_tracks_genre")]
    [InlineData("UPDATE tracks SET status = 'published' WHERE id = @id", "ck_tracks_status")]
    [InlineData("UPDATE tracks SET isrc = 'eg-a1b-26-00' WHERE id = @id", "ck_tracks_isrc_format")]
    public async Task CheckConstraints_RefuseValuesThatSkipTheValidators(string sql, string constraint)
    {
        // Arrange
        var artist = await NewArtistAsync();
        var track = NewTrack(artist);
        await using var context = postgres.CreateContext();
        context.Add(track);
        await context.SaveChangesAsync(Ct);

        // Act
        var ex = await Should.ThrowAsync<PostgresException>(() =>
            context.Database.ExecuteSqlRawAsync(sql, [new NpgsqlParameter("id", track.Id)], Ct));

        // Assert
        ex.ConstraintName.ShouldBe(constraint);
    }

    [Fact]
    public async Task UnknownArtist_IsRefusedByTheForeignKey()
    {
        await using var context = postgres.CreateContext();
        context.Add(Track.Create("T", Guid.NewGuid(), NewIsrc(), "Rap", new DateOnly(2026, 1, 1), Now));

        var ex = await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync(Ct));
        ex.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task SameIsrc_TenParallelInserts_OnlyOneIsStored()
    {
        // Arrange
        var artist = await NewArtistAsync();
        var isrc = NewIsrc();

        // Act
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 10).Select(async i =>
        {
            await using var context = postgres.CreateContext();
            context.Add(NewTrack(artist, $"Copy {i}", isrc: isrc));
            try
            {
                await context.SaveChangesAsync(Ct);
                return (string?)null;
            }
            catch (DbUpdateException e) when (e.InnerException is PostgresException p)
            {
                return p.ConstraintName;
            }
        }));

        // Assert
        outcomes.Count(o => o is null).ShouldBe(1);
        outcomes.Where(o => o is not null).ShouldAllBe(o => o == TrackConfiguration.IsrcIndex);
        await using var read = postgres.CreateContext();
        (await read.Set<Track>().CountAsync(t => t.Isrc == isrc, Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task ListSpecs_FilterOrderAndProjectInSql()
    {
        // Arrange: a dedicated artist so other tests' rows do not interfere
        var artist = await NewArtistAsync("Wegz");
        var tracks = new[]
        {
            NewTrack(artist, "beta", "Rap", 2025),
            NewTrack(artist, "Alpha", "Rap", 2025),
            NewTrack(artist, "Newest", "R&B", 2027),
            NewTrack(artist, "Submitted", "Rap", 2024),
        };
        tracks[3].ChangeStatus(TrackStatus.Submitted, Now);
        await using (var write = postgres.CreateContext())
        {
            write.AddRange(tracks);
            await write.SaveChangesAsync(Ct);
        }

        await using var read = postgres.CreateContext();
        var repository = new EfRepository<Track>(read);

        // Act
        var all = await repository.ListAsync(new TracksPageSpec(new TrackFilter(artist.Id), 1, 20), Ct);
        var rap = await repository.ListAsync(new TracksPageSpec(new TrackFilter(artist.Id, "Rap", TrackStatus.Draft), 1, 20), Ct);
        var count = await repository.CountAsync(new TracksCountSpec(new TrackFilter(artist.Id, "Rap")), Ct);
        var detail = await repository.FirstOrDefaultAsync(new TrackDetailSpec(tracks[3].Id), Ct);

        // Assert
        all.Select(t => t.Title).ShouldBe(["Newest", "Alpha", "beta", "Submitted"]);
        all.ShouldAllBe(t => t.ArtistName == "Wegz");
        all[0].Isrc.ShouldBe(Isrc.Format(tracks[2].Isrc));
        rap.Select(t => t.Title).ShouldBe(["Alpha", "beta"]);
        count.ShouldBe(3);
        detail!.Status.ShouldBe(TrackStatus.Submitted);
        detail.AllowedNextStatuses.ShouldBe([TrackStatus.Draft, TrackStatus.Distributed]);
        detail.Distributions.ShouldBeEmpty();
    }
}
