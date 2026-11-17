using Ardalis.Result;
using Ardalis.Specification;
using TrackManagement.Application.Common.Persistence;
using TrackManagement.Application.Features.Tracks.Commands.ChangeTrackStatus;
using TrackManagement.Domain.Tracks;
using TrackManagement.UnitTests.Application.Artists;

namespace TrackManagement.UnitTests.Application.Tracks;

public sealed class ChangeTrackStatusTests
{
    private static readonly DateTimeOffset Later = new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);
    private readonly IRepository<Track> _tracks = Substitute.For<IRepository<Track>>();
    private readonly List<Track> _data = [];
    private readonly ChangeTrackStatusCommandHandler _sut;

    public ChangeTrackStatusTests()
    {
        _tracks.FirstOrDefaultAsync(Arg.Any<ISpecification<Track>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((ISpecification<Track>)ci[0]).Evaluate(_data).FirstOrDefault());
        _sut = new ChangeTrackStatusCommandHandler(_tracks, new FixedTimeProvider(Later));
    }

    private Track Add(TrackStatus status)
    {
        var track = TrackTestData.Track(TrackTestData.Artist("Wegz", "wegz@example.com"), "El Bakht", "EGW1Z2100001", status: status);
        _data.Add(track);
        return track;
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Validator_Missing_IsRequired(string status) =>
        new ChangeTrackStatusCommandValidator().Validate(new ChangeTrackStatusCommand(Guid.NewGuid(), status))
            .Errors.ShouldHaveSingleItem().ErrorMessage.ShouldBe("Status is required.");

    [Theory]
    [InlineData("published")]
    [InlineData("Draft")]
    [InlineData("1")]
    public void Validator_Unknown_ListsTheValues(string status) =>
        new ChangeTrackStatusCommandValidator().Validate(new ChangeTrackStatusCommand(Guid.NewGuid(), status))
            .Errors.ShouldHaveSingleItem().ErrorMessage.ShouldBe("Status must be one of: draft, submitted, distributed.");

    [Fact]
    public async Task Handle_UnknownTrack_ReturnsNotFound()
    {
        var result = await _sut.Handle(new ChangeTrackStatusCommand(Guid.NewGuid(), "submitted"), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Handle_Allowed_ChangesTrack_ReturnsDetail_WithoutSaving()
    {
        // Arrange
        var track = Add(TrackStatus.Submitted);

        // Act
        var result = await _sut.Handle(new ChangeTrackStatusCommand(track.Id, "distributed"), TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(ResultStatus.Ok);
        result.Value.Status.ShouldBe(TrackStatus.Distributed);
        result.Value.AllowedNextStatuses.ShouldBeEmpty();
        result.Value.UpdatedAtUtc.ShouldBe(Later.UtcDateTime);
        result.Value.ArtistName.ShouldBe("Wegz");
        track.Status.ShouldBe(TrackStatus.Distributed);
        await _tracks.DidNotReceiveWithAnyArgs().SaveChangesAsync(TestContext.Current.CancellationToken);
        await _tracks.DidNotReceiveWithAnyArgs().UpdateAsync(default!, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(TrackStatus.Draft, "draft", "The track is already draft.")]
    [InlineData(TrackStatus.Draft, "distributed", "A draft track must be submitted before it can be distributed.")]
    [InlineData(TrackStatus.Distributed, "draft", "A distributed track is final; its status cannot change.")]
    public async Task Handle_Refused_ReturnsConflictWithTheDomainMessage(TrackStatus from, string to, string message)
    {
        var track = Add(from);

        var result = await _sut.Handle(new ChangeTrackStatusCommand(track.Id, to), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ResultStatus.Conflict);
        result.Errors.ShouldBe([message]);
        track.Status.ShouldBe(from);
    }
}
