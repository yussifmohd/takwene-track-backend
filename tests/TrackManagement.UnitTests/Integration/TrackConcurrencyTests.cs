using Microsoft.EntityFrameworkCore;
using TrackManagement.Domain.Artists;
using TrackManagement.Domain.Tracks;

namespace TrackManagement.UnitTests.Integration;

/// <summary>Two admins change the same track from the same loaded version: only one save may win (xmin).</summary>
[Collection(PostgresDatabase.Name)]
[Trait("Category", "Integration")]
public sealed class TrackConcurrencyTests(PostgresFixture postgres)
{
    private static readonly DateTime Now = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task WithdrawAndDistributeAtTheSameTime_OnlyOneWins_AndDistributedNeverGoesBack()
    {
        // Arrange: a submitted track
        var artist = Artist.Create("Wegz", "EG", $"{Guid.NewGuid():N}@example.com", Now);
        var track = Track.Create("El Bakht", artist.Id, TrackPersistenceTests.NewIsrc(), "Rap", new DateOnly(2026, 1, 1), Now);
        track.ChangeStatus(TrackStatus.Submitted, Now);
        await using (var setup = postgres.CreateContext())
        {
            setup.AddRange(artist, track);
            await setup.SaveChangesAsync(Ct);
        }

        // Both admins load the same version
        await using var first = postgres.CreateContext();
        await using var second = postgres.CreateContext();
        var firstCopy = await first.Set<Track>().SingleAsync(t => t.Id == track.Id, Ct);
        var secondCopy = await second.Set<Track>().SingleAsync(t => t.Id == track.Id, Ct);

        // Act: one distributes and saves first; the other withdraws from the stale copy
        firstCopy.Distribute([TrackManagement.Domain.Dsps.Dsp.SpotifyId], Now).Outcome.ShouldBe(DistributeOutcome.Changed);
        await first.SaveChangesAsync(Ct);
        secondCopy.ChangeStatus(TrackStatus.Draft, Now).IsChanged.ShouldBeTrue(); // allowed from what it saw
        var stale = () => second.SaveChangesAsync(Ct);

        // Assert
        await Should.ThrowAsync<DbUpdateConcurrencyException>(stale);
        await using var read = postgres.CreateContext();
        (await read.Set<Track>().AsNoTracking().SingleAsync(t => t.Id == track.Id, Ct)).Status.ShouldBe(TrackStatus.Distributed);
    }

    [Fact]
    public async Task SequentialChanges_EachSeeTheNewVersion()
    {
        // Arrange
        var artist = Artist.Create("Wegz", "EG", $"{Guid.NewGuid():N}@example.com", Now);
        var track = Track.Create("Dorak Gai", artist.Id, TrackPersistenceTests.NewIsrc(), "Rap", new DateOnly(2026, 1, 1), Now);
        await using (var setup = postgres.CreateContext())
        {
            setup.AddRange(artist, track);
            await setup.SaveChangesAsync(Ct);
        }

        // Act: two requests one after the other, each loading fresh
        foreach (var target in new[] { TrackStatus.Submitted, TrackStatus.Draft, TrackStatus.Submitted })
        {
            await using var context = postgres.CreateContext();
            var loaded = await context.Set<Track>().SingleAsync(t => t.Id == track.Id, Ct);
            loaded.ChangeStatus(target, Now).IsChanged.ShouldBeTrue();
            await Should.NotThrowAsync(() => context.SaveChangesAsync(Ct));
        }

        // Assert
        await using var read = postgres.CreateContext();
        (await read.Set<Track>().AsNoTracking().SingleAsync(t => t.Id == track.Id, Ct)).Status.ShouldBe(TrackStatus.Submitted);
    }
}
