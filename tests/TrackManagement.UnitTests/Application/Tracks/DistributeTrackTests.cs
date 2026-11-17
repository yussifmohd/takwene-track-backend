using Ardalis.Result;
using Ardalis.Specification;
using TrackManagement.Application.Common.Persistence;
using TrackManagement.Application.Features.Tracks.Commands.DistributeTrack;
using TrackManagement.Domain.Dsps;
using TrackManagement.Domain.Tracks;
using TrackManagement.UnitTests.Application.Artists;

namespace TrackManagement.UnitTests.Application.Tracks;

public sealed class DistributeTrackTests
{
    private static readonly DateTimeOffset Later = new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);
    private readonly IRepository<Track> _tracks = Substitute.For<IRepository<Track>>();
    private readonly List<Track> _data = [];
    private readonly DistributeTrackCommandHandler _sut;

    public DistributeTrackTests()
    {
        _tracks.FirstOrDefaultAsync(Arg.Any<ISpecification<Track>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((ISpecification<Track>)ci[0]).Evaluate(_data).FirstOrDefault());
        _sut = new DistributeTrackCommandHandler(_tracks, TrackTestData.Dsps(), new FixedTimeProvider(Later));
    }

    private Track Add(TrackStatus status)
    {
        var track = TrackTestData.Track(TrackTestData.Artist("Wegz", "wegz@example.com"), "El Bakht", "EGW1Z2100001", status: status);
        _data.Add(track);
        return track;
    }

    private static List<FluentValidation.Results.ValidationFailure> Validate(IReadOnlyList<Guid>? ids) =>
        new DistributeTrackCommandValidator().Validate(new DistributeTrackCommand(Guid.NewGuid(), ids)).Errors;

    [Fact]
    public void Validator_MissingOrEmpty_IsRequired()
    {
        Validate(null).ShouldHaveSingleItem().ErrorMessage.ShouldBe("Choose at least one platform.");
        var empty = Validate([]).ShouldHaveSingleItem();
        empty.ErrorMessage.ShouldBe("Choose at least one platform.");
        empty.PropertyName.ShouldBe("DspIds");
    }

    [Fact]
    public void Validator_TwentyDistinct_Accepted_TwentyOne_Refused()
    {
        Validate(Enumerable.Range(0, 20).Select(_ => Guid.NewGuid()).ToList()).ShouldBeEmpty();
        Validate(Enumerable.Range(0, 21).Select(_ => Guid.NewGuid()).ToList())
            .ShouldHaveSingleItem().ErrorMessage.ShouldBe("Send to at most 20 different platforms at once.");
    }

    [Fact]
    public void Validator_RepeatsCountOnce() =>
        Validate(Enumerable.Range(0, 25).Select(i => i % 2 == 0 ? Dsp.SpotifyId : Dsp.AppleMusicId).ToList()).ShouldBeEmpty();

    [Fact]
    public async Task Handle_UnknownTrack_ReturnsNotFound()
    {
        var result = await _sut.Handle(new DistributeTrackCommand(Guid.NewGuid(), [Dsp.SpotifyId]), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Handle_UnknownPlatform_IsInvalidOnDspIds_AndNothingChanges()
    {
        var track = Add(TrackStatus.Submitted);
        var unknown = Guid.NewGuid();

        var result = await _sut.Handle(new DistributeTrackCommand(track.Id, [Dsp.SpotifyId, unknown]), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ResultStatus.Invalid);
        var error = result.ValidationErrors.ShouldHaveSingleItem();
        error.Identifier.ShouldBe("DspIds");
        error.ErrorMessage.ShouldBe($"Unknown platform: {unknown}.");
        track.Distributions.ShouldBeEmpty();
        track.Status.ShouldBe(TrackStatus.Submitted);
    }

    [Fact]
    public async Task Handle_Draft_ReturnsConflict()
    {
        var track = Add(TrackStatus.Draft);

        var result = await _sut.Handle(new DistributeTrackCommand(track.Id, [Dsp.SpotifyId]), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ResultStatus.Conflict);
        result.Errors.ShouldBe(["A draft track must be submitted before it can be distributed."]);
    }

    [Fact]
    public async Task Handle_Submitted_ReturnsDistributedDetail_DeliveriesByName_WithoutSaving()
    {
        // Arrange
        var track = Add(TrackStatus.Submitted);

        // Act
        var result = await _sut.Handle(
            new DistributeTrackCommand(track.Id, [Dsp.YouTubeMusicId, Dsp.SpotifyId, Dsp.AppleMusicId]), TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(ResultStatus.Ok);
        result.Value.Status.ShouldBe(TrackStatus.Distributed);
        result.Value.AllowedNextStatuses.ShouldBeEmpty();
        result.Value.CanDistribute.ShouldBeTrue();
        result.Value.UpdatedAtUtc.ShouldBe(Later.UtcDateTime);
        result.Value.Distributions.Select(d => d.DspName).ShouldBe(["Apple Music", "Spotify", "YouTube Music"]);
        result.Value.Distributions.ShouldAllBe(d =>
            d.Status == DistributionStatus.Pending && d.SubmittedAtUtc == Later.UtcDateTime && d.AnsweredAtUtc == null
            && d.AllowedAnswers.Count == 2);
        await _tracks.DidNotReceiveWithAnyArgs().SaveChangesAsync(TestContext.Current.CancellationToken);
        await _tracks.DidNotReceiveWithAnyArgs().UpdateAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_NothingToSend_IsSuccess_WithoutChanges()
    {
        var track = Add(TrackStatus.Distributed); // already pending on Spotify
        var before = track.UpdatedAtUtc;

        var result = await _sut.Handle(new DistributeTrackCommand(track.Id, [Dsp.SpotifyId, Dsp.SpotifyId]), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ResultStatus.Ok);
        result.Value.Distributions.ShouldHaveSingleItem().SubmittedAtUtc.ShouldBe(TrackTestData.Now);
        track.UpdatedAtUtc.ShouldBe(before);
    }
}
