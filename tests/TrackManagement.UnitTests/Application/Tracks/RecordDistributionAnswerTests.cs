using Ardalis.Result;
using Ardalis.Specification;
using TrackManagement.Application.Common.Persistence;
using TrackManagement.Application.Features.Tracks.Commands.RecordDistributionAnswer;
using TrackManagement.Domain.Dsps;
using TrackManagement.Domain.Tracks;
using TrackManagement.UnitTests.Application.Artists;

namespace TrackManagement.UnitTests.Application.Tracks;

public sealed class RecordDistributionAnswerTests
{
    private static readonly DateTimeOffset Later = new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);
    private readonly IRepository<Track> _tracks = Substitute.For<IRepository<Track>>();
    private readonly List<Track> _data = [];
    private readonly RecordDistributionAnswerCommandHandler _sut;

    public RecordDistributionAnswerTests()
    {
        _tracks.FirstOrDefaultAsync(Arg.Any<ISpecification<Track>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((ISpecification<Track>)ci[0]).Evaluate(_data).FirstOrDefault());
        _sut = new RecordDistributionAnswerCommandHandler(_tracks, TrackTestData.Dsps(), new FixedTimeProvider(Later));
    }

    private Track AddDistributed()
    {
        var track = TrackTestData.Track(TrackTestData.Artist("Wegz", "wegz@example.com"), "El Bakht", "EGW1Z2100001", status: TrackStatus.Distributed);
        _data.Add(track);
        return track;
    }

    [Theory]
    [InlineData("")]
    [InlineData("pending")]
    [InlineData("Live")]
    [InlineData("distributed")]
    public void Validator_OnlyLiveOrRejected(string status) =>
        new RecordDistributionAnswerCommandValidator().Validate(new RecordDistributionAnswerCommand(Guid.NewGuid(), Guid.NewGuid(), status))
            .Errors.ShouldHaveSingleItem().ErrorMessage.ShouldBe("Status must be live or rejected.");

    [Theory]
    [InlineData("live")]
    [InlineData("rejected")]
    public void Validator_Answers_AreValid(string status) =>
        new RecordDistributionAnswerCommandValidator().Validate(new RecordDistributionAnswerCommand(Guid.NewGuid(), Guid.NewGuid(), status))
            .IsValid.ShouldBeTrue();

    [Fact]
    public async Task Handle_UnknownTrack_ReturnsNotFound()
    {
        var result = await _sut.Handle(new RecordDistributionAnswerCommand(Guid.NewGuid(), Dsp.SpotifyId, "live"), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ResultStatus.NotFound);
        result.Errors.ShouldBe(["No track has this id."]);
    }

    [Fact]
    public async Task Handle_PlatformNeverSentTo_ReturnsNotFound()
    {
        var track = AddDistributed();

        var result = await _sut.Handle(new RecordDistributionAnswerCommand(track.Id, Dsp.AppleMusicId, "live"), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ResultStatus.NotFound);
        result.Errors.ShouldBe(["This track has not been sent to this platform."]);
    }

    [Fact]
    public async Task Handle_Pending_RecordsTheAnswer()
    {
        var track = AddDistributed();

        var result = await _sut.Handle(new RecordDistributionAnswerCommand(track.Id, Dsp.SpotifyId, "live"), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ResultStatus.Ok);
        var delivery = result.Value.Distributions.ShouldHaveSingleItem();
        delivery.DspName.ShouldBe("Spotify");
        delivery.Status.ShouldBe(DistributionStatus.Live);
        delivery.AnsweredAtUtc.ShouldBe(Later.UtcDateTime);
        delivery.AllowedAnswers.ShouldBeEmpty();
        result.Value.Status.ShouldBe(TrackStatus.Distributed);
        result.Value.UpdatedAtUtc.ShouldBe(Later.UtcDateTime);
    }

    [Theory]
    [InlineData("live", "live", "The delivery is already live.")]
    [InlineData("live", "rejected", "A live delivery is final.")]
    [InlineData("rejected", "rejected", "The delivery is already rejected.")]
    [InlineData("rejected", "live", "A rejected delivery must be sent again with Distribute before the platform can answer.")]
    public async Task Handle_Refused_ReturnsConflictWithTheDomainMessage(string first, string second, string message)
    {
        var track = AddDistributed();
        await _sut.Handle(new RecordDistributionAnswerCommand(track.Id, Dsp.SpotifyId, first), TestContext.Current.CancellationToken);

        var result = await _sut.Handle(new RecordDistributionAnswerCommand(track.Id, Dsp.SpotifyId, second), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ResultStatus.Conflict);
        result.Errors.ShouldBe([message]);
    }
}
