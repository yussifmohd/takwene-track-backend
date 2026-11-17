using TrackManagement.Domain.Dsps;
using TrackManagement.Domain.Tracks;

namespace TrackManagement.UnitTests.Domain.Tracks;

public sealed class TrackRecordAnswerTests
{
    private static readonly DateTime T0 = new(2026, 11, 17, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T1 = T0.AddHours(1);

    private static Track Distributed()
    {
        var track = Track.Create("Title", Guid.NewGuid(), "EGA1B2600001", "Rap", new DateOnly(2026, 12, 1), T0);
        track.ChangeStatus(TrackStatus.Submitted, T0);
        track.Distribute([Dsp.SpotifyId], T0);
        return track;
    }

    [Theory]
    [InlineData(DistributionStatus.Live)]
    [InlineData(DistributionStatus.Rejected)]
    public void Pending_TakesTheAnswer_StampsTheTrack_KeepsItsStatus(DistributionStatus answer)
    {
        var track = Distributed();

        var result = track.RecordAnswer(Dsp.SpotifyId, answer, T1);

        result.ShouldNotBeNull().IsChanged.ShouldBeTrue();
        var delivery = track.Distributions.Single();
        delivery.Status.ShouldBe(answer);
        delivery.AnsweredAtUtc.ShouldBe(T1);
        delivery.SubmittedAtUtc.ShouldBe(T0);
        track.UpdatedAtUtc.ShouldBe(T1);
        track.Status.ShouldBe(TrackStatus.Distributed);
    }

    [Fact]
    public void Refused_ChangesNothing()
    {
        var track = Distributed();
        track.RecordAnswer(Dsp.SpotifyId, DistributionStatus.Live, T0);

        var result = track.RecordAnswer(Dsp.SpotifyId, DistributionStatus.Rejected, T1);

        result.ShouldNotBeNull().Outcome.ShouldBe(StatusChangeOutcome.NotAllowed);
        result.Value.Message.ShouldBe("A live delivery is final.");
        track.Distributions.Single().Status.ShouldBe(DistributionStatus.Live);
        track.UpdatedAtUtc.ShouldBe(T0);
    }

    [Fact]
    public void PlatformNeverSentTo_IsNull() => Distributed().RecordAnswer(Dsp.AppleMusicId, DistributionStatus.Live, T1).ShouldBeNull();
}
