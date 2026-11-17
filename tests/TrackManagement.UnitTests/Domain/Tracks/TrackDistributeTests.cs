using TrackManagement.Domain.Dsps;
using TrackManagement.Domain.Tracks;

namespace TrackManagement.UnitTests.Domain.Tracks;

public sealed class TrackDistributeTests
{
    private static readonly DateTime T0 = new(2026, 11, 17, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T1 = T0.AddHours(1);
    private static readonly DateTime T2 = T0.AddHours(2);
    private static readonly Guid Spotify = Dsp.SpotifyId;
    private static readonly Guid Apple = Dsp.AppleMusicId;
    private static readonly Guid YouTube = Dsp.YouTubeMusicId;

    private static Track NewTrack(TrackStatus status = TrackStatus.Submitted)
    {
        var track = Track.Create("Title", Guid.NewGuid(), "EGA1B2600001", "Rap", new DateOnly(2026, 12, 1), T0);
        if (status != TrackStatus.Draft)
        {
            track.ChangeStatus(TrackStatus.Submitted, T0);
        }

        return track;
    }

    private static TrackDistribution Delivery(Track track, Guid dspId) => track.Distributions.Single(d => d.DspId == dspId);

    [Fact]
    public void Draft_IsRefused_AndNothingChanges()
    {
        var track = NewTrack(TrackStatus.Draft);

        var result = track.Distribute([Spotify], T1);

        result.Outcome.ShouldBe(DistributeOutcome.NotAllowed);
        result.Message.ShouldBe("A draft track must be submitted before it can be distributed.");
        track.Distributions.ShouldBeEmpty();
        track.Status.ShouldBe(TrackStatus.Draft);
    }

    [Fact]
    public void Submitted_CreatesPendingDeliveries_AndBecomesDistributed()
    {
        var track = NewTrack();

        var result = track.Distribute([Spotify, Apple], T1);

        result.Outcome.ShouldBe(DistributeOutcome.Changed);
        result.SentDspIds.ShouldBe([Spotify, Apple]);
        track.Status.ShouldBe(TrackStatus.Distributed);
        track.UpdatedAtUtc.ShouldBe(T1);
        track.Distributions.Count.ShouldBe(2);
        track.Distributions.ShouldAllBe(d => d.Status == DistributionStatus.Pending && d.SubmittedAtUtc == T1 && d.AnsweredAtUtc == null && d.TrackId == track.Id);
    }

    [Fact]
    public void RepeatedIds_CountOnce()
    {
        var track = NewTrack();

        var result = track.Distribute([Spotify, Spotify, Spotify], T1);

        result.SentDspIds.ShouldBe([Spotify]);
        track.Distributions.Count.ShouldBe(1);
    }

    [Fact]
    public void Distributed_KeepsPendingAndLive_AddsNew_ResendsRejected()
    {
        var track = NewTrack();
        track.Distribute([Spotify, Apple, YouTube], T0);
        track.RecordAnswer(Apple, DistributionStatus.Live, T1);
        track.RecordAnswer(YouTube, DistributionStatus.Rejected, T1);

        var result = track.Distribute([Spotify, Apple, YouTube], T2);

        result.Outcome.ShouldBe(DistributeOutcome.Changed);
        result.SentDspIds.ShouldBe([YouTube]);
        Delivery(track, Spotify).SubmittedAtUtc.ShouldBe(T0);
        Delivery(track, Spotify).Status.ShouldBe(DistributionStatus.Pending);
        Delivery(track, Apple).Status.ShouldBe(DistributionStatus.Live);
        Delivery(track, Apple).AnsweredAtUtc.ShouldBe(T1);
        var resent = Delivery(track, YouTube);
        resent.Status.ShouldBe(DistributionStatus.Pending);
        resent.SubmittedAtUtc.ShouldBe(T2);
        resent.AnsweredAtUtc.ShouldBeNull();
        track.Status.ShouldBe(TrackStatus.Distributed);
        track.UpdatedAtUtc.ShouldBe(T2);
    }

    [Fact]
    public void NothingToSend_IsUnchanged_AndDoesNotStampTheTrack()
    {
        var track = NewTrack();
        track.Distribute([Spotify], T0);
        track.RecordAnswer(Spotify, DistributionStatus.Live, T1);

        var result = track.Distribute([Spotify], T2);

        result.Outcome.ShouldBe(DistributeOutcome.Unchanged);
        result.SentDspIds.ShouldBeEmpty();
        track.UpdatedAtUtc.ShouldBe(T1);
    }

    [Fact]
    public void EmptyList_IsAGuardError() => Should.Throw<ArgumentException>(() => NewTrack().Distribute([], T1));

    [Fact]
    public void AfterDistributing_TheStatusRuleAllowsNothing_AndWithoutDeliveriesPatchIsRefused()
    {
        var withoutDeliveries = NewTrack();
        withoutDeliveries.ChangeStatus(TrackStatus.Distributed, T1).Outcome.ShouldBe(StatusChangeOutcome.NotAllowed);
        withoutDeliveries.AllowedNextStatuses.ShouldBe([TrackStatus.Draft]);

        var distributed = NewTrack();
        distributed.Distribute([Spotify], T1);
        distributed.AllowedNextStatuses.ShouldBeEmpty();
        distributed.CanDistribute.ShouldBeTrue();
        NewTrack(TrackStatus.Draft).CanDistribute.ShouldBeFalse();
    }
}
