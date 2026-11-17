using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TrackManagement.Application.Features.Tracks.Specifications;
using TrackManagement.Domain.Artists;
using TrackManagement.Domain.Dsps;
using TrackManagement.Domain.Tracks;
using TrackManagement.Infrastructure.Persistence;

namespace TrackManagement.UnitTests.Integration;

/// <summary>Deliveries on real PostgreSQL: keys, checks, projection, races between admins, read time.</summary>
[Collection(PostgresDatabase.Name)]
[Trait("Category", "Integration")]
public sealed class DistributionPersistenceTests(PostgresFixture postgres)
{
    private static readonly DateTime Now = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Track> NewSubmittedTrackAsync(Action<Track>? before = null)
    {
        var artist = Artist.Create("Wegz", "EG", $"{Guid.NewGuid():N}@example.com", Now);
        var track = Track.Create("El Bakht", artist.Id, TrackPersistenceTests.NewIsrc(), "Rap", new DateOnly(2026, 1, 1), Now);
        track.ChangeStatus(TrackStatus.Submitted, Now);
        before?.Invoke(track);
        await using var setup = postgres.CreateContext();
        setup.AddRange(artist, track);
        await setup.SaveChangesAsync(Ct);
        return track;
    }

    private static Task<Track> LoadAsync(AppDbContext context, Guid id) =>
        context.Set<Track>().Include(t => t.Distributions).SingleAsync(t => t.Id == id, Ct);

    private async Task<List<TrackDistribution>> DeliveriesAsync(Guid id)
    {
        await using var read = postgres.CreateContext();
        return await read.Set<TrackDistribution>().AsNoTracking().Where(d => d.TrackId == id).ToListAsync(Ct);
    }

    [Fact]
    public async Task Deliveries_RoundTrip_AndTheDetailProjectsThemByName()
    {
        // Arrange
        var track = await NewSubmittedTrackAsync();
        await using (var context = postgres.CreateContext())
        {
            var loaded = await LoadAsync(context, track.Id);
            loaded.Distribute([Dsp.YouTubeMusicId, Dsp.SpotifyId, Dsp.AppleMusicId], Now);
            loaded.RecordAnswer(Dsp.SpotifyId, DistributionStatus.Live, Now.AddHours(1));
            await context.SaveChangesAsync(Ct);
        }

        // Act
        await using var read = postgres.CreateContext();
        var detail = await new EfRepository<Track>(read).FirstOrDefaultAsync(new TrackDetailSpec(track.Id), Ct);

        // Assert
        detail.ShouldNotBeNull();
        detail.Status.ShouldBe(TrackStatus.Distributed);
        detail.AllowedNextStatuses.ShouldBeEmpty();
        detail.Distributions.Select(d => d.DspName).ShouldBe(["Apple Music", "Spotify", "YouTube Music"]);
        var spotify = detail.Distributions.Single(d => d.DspId == Dsp.SpotifyId);
        spotify.Status.ShouldBe(DistributionStatus.Live);
        spotify.AnsweredAtUtc.ShouldBe(Now.AddHours(1));
        spotify.AllowedAnswers.ShouldBeEmpty();
        detail.Distributions.Single(d => d.DspId == Dsp.AppleMusicId).AllowedAnswers.ShouldBe([DistributionStatus.Live, DistributionStatus.Rejected]);
    }

    [Theory]
    [InlineData("UPDATE track_distributions SET status = 'sent', answered_at_utc = now() WHERE track_id = @id", "ck_track_distributions_status")]
    [InlineData("UPDATE track_distributions SET answered_at_utc = now() WHERE track_id = @id", "ck_track_distributions_answered")]
    [InlineData("UPDATE track_distributions SET status = 'live' WHERE track_id = @id", "ck_track_distributions_answered")]
    public async Task CheckConstraints_KeepStatusAndAnswerTimeConsistent(string sql, string constraint)
    {
        var track = await NewSubmittedTrackAsync(t => t.Distribute([Dsp.SpotifyId], Now));
        await using var context = postgres.CreateContext();

        var ex = await Should.ThrowAsync<PostgresException>(() =>
            context.Database.ExecuteSqlRawAsync(sql, [new NpgsqlParameter("id", track.Id)], Ct));

        ex.ConstraintName.ShouldBe(constraint);
    }

    [Fact]
    public async Task UnknownPlatform_IsRefusedByTheForeignKey()
    {
        var track = await NewSubmittedTrackAsync();
        await using var context = postgres.CreateContext();
        var loaded = await LoadAsync(context, track.Id);
        loaded.Distribute([Guid.NewGuid()], Now);

        var ex = await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync(Ct));

        ex.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task TwoAdminsSendTheSameNewPlatform_OnlyOneSaves_AndThereIsOneDelivery()
    {
        // Arrange: a distributed track; both admins load the same version
        var track = await NewSubmittedTrackAsync(t => t.Distribute([Dsp.SpotifyId], Now));
        await using var first = postgres.CreateContext();
        await using var second = postgres.CreateContext();
        var firstCopy = await LoadAsync(first, track.Id);
        var secondCopy = await LoadAsync(second, track.Id);

        // Act
        firstCopy.Distribute([Dsp.AppleMusicId], Now.AddMinutes(1)).Outcome.ShouldBe(DistributeOutcome.Changed);
        secondCopy.Distribute([Dsp.AppleMusicId], Now.AddMinutes(2)).Outcome.ShouldBe(DistributeOutcome.Changed);
        await first.SaveChangesAsync(Ct);
        var stale = () => second.SaveChangesAsync(Ct);

        // Assert: refused (primary key or row version, both 409 at the API) and one row
        await Should.ThrowAsync<DbUpdateException>(stale);
        var apple = (await DeliveriesAsync(track.Id)).Where(d => d.DspId == Dsp.AppleMusicId).ShouldHaveSingleItem();
        apple.SubmittedAtUtc.ShouldBe(Now.AddMinutes(1));
    }

    [Fact]
    public async Task DistributeAndWithdrawAtTheSameTime_OnlyOneWins_NeverDistributedWithoutDeliveries()
    {
        var track = await NewSubmittedTrackAsync();
        await using var first = postgres.CreateContext();
        await using var second = postgres.CreateContext();
        var withdrawing = await LoadAsync(first, track.Id);
        var distributing = await LoadAsync(second, track.Id);

        withdrawing.ChangeStatus(TrackStatus.Draft, Now).IsChanged.ShouldBeTrue();
        await first.SaveChangesAsync(Ct);
        distributing.Distribute([Dsp.SpotifyId], Now).Outcome.ShouldBe(DistributeOutcome.Changed);

        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(Ct));
        await using var read = postgres.CreateContext();
        (await read.Set<Track>().AsNoTracking().SingleAsync(t => t.Id == track.Id, Ct)).Status.ShouldBe(TrackStatus.Draft);
        (await DeliveriesAsync(track.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task AnswerAndResendAtTheSameTime_OnlyOneWins()
    {
        // Arrange: Spotify rejected; one admin sends it again while another (stale) still sees the old state
        var track = await NewSubmittedTrackAsync(t =>
        {
            t.Distribute([Dsp.SpotifyId, Dsp.AppleMusicId], Now);
            t.RecordAnswer(Dsp.SpotifyId, DistributionStatus.Rejected, Now);
        });
        await using var first = postgres.CreateContext();
        await using var second = postgres.CreateContext();
        var resending = await LoadAsync(first, track.Id);
        var answering = await LoadAsync(second, track.Id);

        // Act: the re-send is saved first; the stale copy records Apple Music's answer
        resending.Distribute([Dsp.SpotifyId], Now.AddHours(1)).Outcome.ShouldBe(DistributeOutcome.Changed);
        await first.SaveChangesAsync(Ct);
        answering.RecordAnswer(Dsp.AppleMusicId, DistributionStatus.Live, Now.AddHours(1))!.Value.IsChanged.ShouldBeTrue();

        // Assert: the stale answer is refused; the re-send stands
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(Ct));
        var rows = await DeliveriesAsync(track.Id);
        rows.Single(d => d.DspId == Dsp.SpotifyId).Status.ShouldBe(DistributionStatus.Pending);
        rows.Single(d => d.DspId == Dsp.AppleMusicId).Status.ShouldBe(DistributionStatus.Pending);
    }

    [Fact]
    public async Task Detail_WithThreeDeliveries_AmongAThousandTracks_ReadsInUnderOneSecond()
    {
        // Arrange: 1,000 tracks, one with three deliveries
        var artist = Artist.Create("Wegz", "EG", $"{Guid.NewGuid():N}@example.com", Now);
        var tracks = Enumerable.Range(0, 1000)
            .Select(i => Track.Create($"Track {i}", artist.Id, TrackPersistenceTests.NewIsrc(), "Rap", new DateOnly(2026, 1, 1), Now))
            .ToList();
        var target = tracks[500];
        target.ChangeStatus(TrackStatus.Submitted, Now);
        target.Distribute([Dsp.SpotifyId, Dsp.AppleMusicId, Dsp.YouTubeMusicId], Now);
        await using (var setup = postgres.CreateContext())
        {
            setup.Add(artist);
            setup.AddRange(tracks);
            await setup.SaveChangesAsync(Ct);
        }

        // Act: warm the model once, then time a fresh read as a request would do
        await using (var warm = postgres.CreateContext())
        {
            await new EfRepository<Track>(warm).FirstOrDefaultAsync(new TrackDetailSpec(tracks[0].Id), Ct);
        }

        var stopwatch = Stopwatch.StartNew();
        await using var read = postgres.CreateContext();
        var detail = await new EfRepository<Track>(read).FirstOrDefaultAsync(new TrackDetailSpec(target.Id), Ct);
        stopwatch.Stop();

        // Assert
        detail.ShouldNotBeNull().Distributions.Count.ShouldBe(3);
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1));
    }
}
