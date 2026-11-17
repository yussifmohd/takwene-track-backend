using Ardalis.Result;
using Ardalis.Specification;
using TrackManagement.Application.Common.Persistence;
using TrackManagement.Application.Features.Artists;
using TrackManagement.Application.Features.Artists.Commands.CreateArtist;
using TrackManagement.Application.Features.Artists.Queries.ListArtists;
using TrackManagement.Application.Features.Artists.Specifications;
using TrackManagement.Domain.Artists;

namespace TrackManagement.UnitTests.Application.Artists;

public sealed class CreateArtistCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
    private readonly IRepository<Artist> _artists = Substitute.For<IRepository<Artist>>();
    private readonly CreateArtistCommandHandler _sut;

    public CreateArtistCommandHandlerTests() => _sut = new CreateArtistCommandHandler(_artists, new FixedTimeProvider(Now));

    [Fact]
    public async Task Handle_AddsNormalizedArtist_ReturnsCreatedDto_WithoutSaving()
    {
        // Act
        var result = await _sut.Handle(new CreateArtistCommand(" Nancy Ajram ", "lb", " Nancy@Example.com "), TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(ResultStatus.Created);
        result.Value.Name.ShouldBe("Nancy Ajram");
        result.Value.CountryCode.ShouldBe("LB");
        result.Value.Email.ShouldBe("Nancy@Example.com");
        result.Value.CreatedAtUtc.ShouldBe(Now.UtcDateTime);
        await _artists.Received(1).AddAsync(Arg.Is<Artist>(a => a.Id == result.Value.Id && a.NormalizedEmail == "NANCY@EXAMPLE.COM"), Arg.Any<CancellationToken>());
        await _artists.DidNotReceiveWithAnyArgs().SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}

public sealed class ListArtistsTests
{
    private static Artist At(string name, int minute, string email) =>
        Artist.Create(name, "EG", email, new DateTime(2026, 11, 17, 12, minute, 0, DateTimeKind.Utc));

    private static readonly Artist[] Data =
    [
        At("beta", 1, "b@x.eg"),
        At("Alpha", 2, "a1@x.eg"),
        At("alpha", 1, "a2@x.eg"), // same name ignoring case, created earlier → first
        At("عمرو دياب", 0, "amr@x.eg"),
    ];

    [Fact]
    public void PageSpec_OrdersByNameIgnoringCase_ThenByCreation()
    {
        var names = new ArtistsPageSpec(1, 20, includeEmail: false).Evaluate(Data).Select(a => a.Name);
        names.ShouldBe(["alpha", "Alpha", "beta", "عمرو دياب"]);
    }

    [Fact]
    public void PageSpec_SkipsAndTakes()
    {
        new ArtistsPageSpec(2, 3, includeEmail: false).Evaluate(Data).Select(a => a.Name).ShouldBe(["عمرو دياب"]);
        new ArtistsPageSpec(3, 3, includeEmail: false).Evaluate(Data).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PageSpec_SelectsEmailOnlyForAdmins(bool includeEmail)
    {
        var emails = new ArtistsPageSpec(1, 20, includeEmail).Evaluate(Data).Select(a => a.Email).ToList();
        if (includeEmail)
        {
            emails.ShouldAllBe(e => e != null);
        }
        else
        {
            emails.ShouldAllBe(e => e == null);
        }
    }

    [Theory]
    [InlineData(0, 20, "Page")]
    [InlineData(1, 0, "PageSize")]
    [InlineData(1, 51, "PageSize")]
    public void Validator_OutOfRange_Fails(int page, int pageSize, string property) =>
        new ListArtistsQueryValidator().Validate(new ListArtistsQuery(page, pageSize, false))
            .Errors.ShouldContain(e => e.PropertyName == property);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 50)]
    [InlineData(99, 20)]
    public void Validator_InRange_Passes(int page, int pageSize) =>
        new ListArtistsQueryValidator().Validate(new ListArtistsQuery(page, pageSize, false)).IsValid.ShouldBeTrue();

    [Fact]
    public async Task Handler_ReturnsPageWithTotals()
    {
        // Arrange
        var repository = Substitute.For<IReadRepository<Artist>>();
        repository.CountAsync(Arg.Any<ArtistsCountSpec>(), Arg.Any<CancellationToken>()).Returns(25);
        repository.ListAsync(Arg.Any<ISpecification<Artist, ArtistDto>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((ISpecification<Artist, ArtistDto>)ci[0]).Evaluate(Data).ToList());

        // Act
        var result = await new ListArtistsQueryHandler(repository).Handle(new ListArtistsQuery(2, 20, false), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Page.ShouldBe(2);
        result.Value.PageSize.ShouldBe(20);
        result.Value.TotalCount.ShouldBe(25);
        result.Value.TotalPages.ShouldBe(2);
    }
}

/// <summary>A clock that always says the same time.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
