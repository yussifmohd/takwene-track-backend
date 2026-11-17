using TrackManagement.Application.Features.Artists.Queries.ListArtists;
using TrackManagement.Application.Features.Artists.Specifications;
using TrackManagement.Domain.Artists;

namespace TrackManagement.UnitTests.Application.Artists;

public sealed class ArtistSearchTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);

    private static readonly Artist[] Data =
    [
        Artist.Create("Amr Diab", "EG", "a@x.eg", Now),
        Artist.Create("عمرو دياب", "EG", "b@x.eg", Now),
        Artist.Create("Tamer Hosny", "EG", "c@x.eg", Now),
        Artist.Create("100% Mahraganat", "EG", "d@x.eg", Now),
    ];

    private static string[] Names(string? search) =>
        new ArtistsPageSpec(1, 20, includeEmail: false, search).Evaluate(Data).Select(a => a.Name).ToArray();

    [Theory]
    [InlineData("amr", "Amr Diab")]
    [InlineData("AMR", "Amr Diab")]
    [InlineData(" diab ", "Amr Diab")]
    [InlineData("عمرو", "عمرو دياب")]
    [InlineData("%", "100% Mahraganat")]
    public void Search_MatchesPartOfTheNameIgnoringCase(string search, string expected) => Names(search).ShouldBe([expected]);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Search_Blank_ListsEveryone(string? search) => Names(search).Length.ShouldBe(4);

    [Fact]
    public void Search_Underscore_IsLiteral() => Names("_").ShouldBeEmpty();

    [Fact]
    public void CountSpec_UsesTheSameSearch() => new ArtistsCountSpec("o").Evaluate(Data).Count().ShouldBe(Names("o").Length);

    [Fact]
    public void Validator_SearchLongerThan100_IsRefused()
    {
        new ListArtistsQueryValidator().Validate(new ListArtistsQuery(1, 20, false, new string('a', 100))).IsValid.ShouldBeTrue();
        new ListArtistsQueryValidator().Validate(new ListArtistsQuery(1, 20, false, new string('a', 101)))
            .Errors.ShouldHaveSingleItem().ErrorMessage.ShouldBe("Search must be at most 100 characters.");
    }
}
