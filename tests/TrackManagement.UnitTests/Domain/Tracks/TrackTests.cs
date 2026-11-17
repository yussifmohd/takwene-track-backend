using TrackManagement.Domain.Tracks;

namespace TrackManagement.UnitTests.Domain.Tracks;

public sealed class TrackTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ArtistId = Guid.NewGuid();

    private static Track NewTrack() => Track.Create(" تملي معاك ", ArtistId, "eg-a1b-26-00001", "Arabic Pop", new DateOnly(2026, 12, 1), Now);

    [Fact]
    public void Create_StartsAsDraft_WithNormalisedValues()
    {
        var track = NewTrack();

        track.Status.ShouldBe(TrackStatus.Draft);
        track.Title.ShouldBe("تملي معاك");
        track.Isrc.ShouldBe("EGA1B2600001");
        track.ArtistId.ShouldBe(ArtistId);
        track.CreatedAtUtc.ShouldBe(Now);
        track.UpdatedAtUtc.ShouldBeNull();
        track.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Create_GuardsInvalidInput()
    {
        var date = new DateOnly(2026, 12, 1);
        Should.Throw<ArgumentException>(() => Track.Create(" ", ArtistId, "EGA1B2600001", "Rap", date, Now));
        Should.Throw<ArgumentException>(() => Track.Create("T", Guid.Empty, "EGA1B2600001", "Rap", date, Now));
        Should.Throw<ArgumentException>(() => Track.Create("T", ArtistId, "EG-A1B", "Rap", date, Now));
        Should.Throw<ArgumentException>(() => Track.Create("T", ArtistId, "EGA1B2600001", "rap", date, Now));
        Should.Throw<ArgumentOutOfRangeException>(() => Track.Create("T", ArtistId, "EGA1B2600001", "Rap", new DateOnly(1899, 12, 31), Now));
        Should.Throw<ArgumentException>(() => Track.Create("T", ArtistId, "EGA1B2600001", "Rap", date, DateTime.SpecifyKind(Now, DateTimeKind.Local)));
    }

    [Fact]
    public void ChangeStatus_Allowed_ChangesStatusAndTime()
    {
        var track = NewTrack();
        var later = Now.AddHours(1);

        var result = track.ChangeStatus(TrackStatus.Submitted, later);

        result.IsChanged.ShouldBeTrue();
        track.Status.ShouldBe(TrackStatus.Submitted);
        track.UpdatedAtUtc.ShouldBe(later);
    }

    [Fact]
    public void ChangeStatus_Refused_ChangesNothing()
    {
        var track = NewTrack();

        var result = track.ChangeStatus(TrackStatus.Distributed, Now.AddHours(1));

        result.Outcome.ShouldBe(StatusChangeOutcome.NotAllowed);
        track.Status.ShouldBe(TrackStatus.Draft);
        track.UpdatedAtUtc.ShouldBeNull();
    }

    [Fact]
    public void ChangeStatus_DistributedIsFinal()
    {
        var track = NewTrack();
        track.ChangeStatus(TrackStatus.Submitted, Now);
        track.ChangeStatus(TrackStatus.Distributed, Now);

        track.ChangeStatus(TrackStatus.Draft, Now).Outcome.ShouldBe(StatusChangeOutcome.NotAllowed);
        track.ChangeStatus(TrackStatus.Submitted, Now).Outcome.ShouldBe(StatusChangeOutcome.NotAllowed);
        track.Status.ShouldBe(TrackStatus.Distributed);
    }
}
