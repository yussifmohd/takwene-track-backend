using Ardalis.Result;
using Ardalis.Specification;
using TrackManagement.Application.Common.Persistence;
using TrackManagement.Application.Common.Validation;
using TrackManagement.Application.Features.Tracks.Commands.CreateTrack;
using TrackManagement.Domain.Artists;
using TrackManagement.Domain.Tracks;
using TrackManagement.UnitTests.Application.Artists;

namespace TrackManagement.UnitTests.Application.Tracks;

public sealed class CreateTrackCommandValidatorTests
{
    private static readonly Artist Known = TrackTestData.Artist("Amr Diab", "amr@example.com");
    private static readonly Track Existing = TrackTestData.Track(Known, "Existing", "EGA1B2600001");
    private readonly IReadRepository<Artist> _artists = Substitute.For<IReadRepository<Artist>>();
    private readonly IReadRepository<Track> _tracks = Substitute.For<IReadRepository<Track>>();
    private readonly CreateTrackCommandValidator _sut;

    public CreateTrackCommandValidatorTests()
    {
        _artists.AnyAsync(Arg.Any<ISpecification<Artist>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((ISpecification<Artist>)ci[0]).Evaluate([Known]).Any());
        _tracks.AnyAsync(Arg.Any<ISpecification<Track>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((ISpecification<Track>)ci[0]).Evaluate([Existing]).Any());
        _sut = new CreateTrackCommandValidator(_artists, _tracks);
    }

    private static CreateTrackCommand Valid() => new("تملي معاك", Known.Id, "EG-A1B-26-00002", "Arabic Pop", "2026-12-01");

    private async Task<Dictionary<string, string[]>> ErrorsAsync(CreateTrackCommand command) =>
        (await _sut.ValidateAsync(command, TestContext.Current.CancellationToken)).Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

    [Fact]
    public async Task Valid_Passes() => (await ErrorsAsync(Valid())).ShouldBeEmpty();

    [Theory]
    [InlineData("2099-01-01")] // future allowed
    [InlineData("1900-01-01")]
    [InlineData("2100-12-31")]
    public async Task ReleaseDate_InRange_Passes(string date) => (await ErrorsAsync(Valid() with { ReleaseDate = date })).ShouldBeEmpty();

    [Fact]
    public async Task EmptyCommand_ReportsEveryFieldOnce()
    {
        var errors = await ErrorsAsync(new CreateTrackCommand(" ", Guid.Empty, "", "", ""));

        errors["Title"].ShouldBe(["Title is required."]);
        errors["ArtistId"].ShouldBe(["Artist is required."]);
        errors["Isrc"].ShouldBe(["ISRC is required."]);
        errors["Genre"].ShouldBe(["Genre is required."]);
        errors["ReleaseDate"].ShouldBe(["Release date is required."]);
    }

    [Fact]
    public async Task Title_CountsCodePoints()
    {
        var at200 = string.Concat(Enumerable.Repeat("ع🎤", 100));
        (await ErrorsAsync(Valid() with { Title = at200 })).ShouldBeEmpty();
        (await ErrorsAsync(Valid() with { Title = at200 + "x" }))["Title"].ShouldBe(["Title must be at most 200 characters."]);
    }

    [Fact]
    public async Task UnknownArtist_IsRefused() =>
        (await ErrorsAsync(Valid() with { ArtistId = Guid.NewGuid() }))["ArtistId"].ShouldBe(["Choose an existing artist."]);

    [Theory]
    [InlineData("EG-A1B-26-0001")]
    [InlineData("EG_A1B_26_00001")]
    [InlineData("EGA1B-26-00001")]
    public async Task Isrc_WrongShape_ExplainsTheFormat(string isrc) =>
        (await ErrorsAsync(Valid() with { Isrc = isrc }))["Isrc"].ShouldBe(["ISRC must look like CC-XXX-YY-NNNNN, e.g. EG-A1B-26-00001."]);

    [Theory]
    [InlineData("EGA1B2600001")]
    [InlineData("eg-a1b-26-00001")]
    [InlineData(" EG-A1B-26-00001 ")]
    public async Task Isrc_UsedByAnotherTrackInAnyForm_IsAConflict(string isrc)
    {
        var result = await _sut.ValidateAsync(Valid() with { Isrc = isrc }, TestContext.Current.CancellationToken);

        var error = result.Errors.ShouldHaveSingleItem();
        error.PropertyName.ShouldBe("Isrc");
        error.ErrorMessage.ShouldBe("Another track already uses this ISRC.");
        error.ErrorCode.ShouldBe(ValidationErrorCodes.Conflict);
    }

    [Theory]
    [InlineData("Pop")]
    [InlineData("r&b")]
    [InlineData("HipHop")]
    public async Task Genre_OutsideTheList_IsRefused(string genre) =>
        (await ErrorsAsync(Valid() with { Genre = genre }))["Genre"]
            .ShouldBe(["Genre must be one of: Arabic Pop, Mahraganat, Khaleeji, Hip-Hop, Electronic, Rap, R&B, Rock."]);

    [Theory]
    [InlineData("2026-02-30", "Release date must be a real date written as YYYY-MM-DD.")]
    [InlineData("01/12/2026", "Release date must be a real date written as YYYY-MM-DD.")]
    [InlineData("2026-12-01T00:00:00", "Release date must be a real date written as YYYY-MM-DD.")]
    [InlineData("1899-12-31", "Release date must be between 1900-01-01 and 2100-12-31.")]
    [InlineData("2101-01-01", "Release date must be between 1900-01-01 and 2100-12-31.")]
    public async Task ReleaseDate_Invalid_IsRefused(string date, string message) =>
        (await ErrorsAsync(Valid() with { ReleaseDate = date }))["ReleaseDate"].ShouldBe([message]);
}

public sealed class CreateTrackCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
    private readonly IRepository<Track> _tracks = Substitute.For<IRepository<Track>>();
    private readonly IReadRepository<Artist> _artists = Substitute.For<IReadRepository<Artist>>();
    private readonly Artist _artist = TrackTestData.Artist("نانسي عجرم", "nancy@example.com");
    private readonly CreateTrackCommandHandler _sut;

    public CreateTrackCommandHandlerTests()
    {
        _artists.FirstOrDefaultAsync(Arg.Any<ISpecification<Artist>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((ISpecification<Artist>)ci[0]).Evaluate([_artist]).FirstOrDefault());
        _sut = new CreateTrackCommandHandler(_tracks, _artists, new FixedTimeProvider(Now));
    }

    [Fact]
    public async Task Handle_AddsDraft_ReturnsCreatedDetail_WithoutSaving()
    {
        // Act
        var result = await _sut.Handle(
            new CreateTrackCommand(" Ya Tabtab ", _artist.Id, "lb-x2y-26-12345", "Arabic Pop", "2026-12-01"), TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(ResultStatus.Created);
        var dto = result.Value;
        dto.Title.ShouldBe("Ya Tabtab");
        dto.ArtistName.ShouldBe("نانسي عجرم");
        dto.Isrc.ShouldBe("LB-X2Y-26-12345");
        dto.Status.ShouldBe(TrackStatus.Draft);
        dto.ReleaseDate.ShouldBe(new DateOnly(2026, 12, 1));
        dto.CreatedAtUtc.ShouldBe(Now.UtcDateTime);
        dto.AllowedNextStatuses.ShouldBe([TrackStatus.Submitted]);
        dto.Distributions.ShouldBeEmpty();
        await _tracks.Received(1).AddAsync(Arg.Is<Track>(t => t.Id == dto.Id && t.Isrc == "LBX2Y2612345"), Arg.Any<CancellationToken>());
        await _tracks.DidNotReceiveWithAnyArgs().SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_ArtistVanished_ReturnsInvalid_AndAddsNothing()
    {
        var result = await _sut.Handle(new CreateTrackCommand("T", Guid.NewGuid(), "EGA1B2600001", "Rap", "2026-12-01"), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ResultStatus.Invalid);
        await _tracks.DidNotReceiveWithAnyArgs().AddAsync(default!, TestContext.Current.CancellationToken);
    }
}
