using Ardalis.Specification;
using TrackManagement.Application.Common.Persistence;
using TrackManagement.Application.Features.Tracks;
using TrackManagement.Application.Features.Tracks.Queries.GetTrack;
using TrackManagement.Application.Features.Tracks.Queries.ListTracks;
using TrackManagement.Application.Features.Tracks.Specifications;
using TrackManagement.Domain.Artists;
using TrackManagement.Domain.Tracks;

namespace TrackManagement.UnitTests.Application.Tracks;

public sealed class ListTracksTests
{
    private static readonly Artist Amr = TrackTestData.Artist("Amr Diab", "amr@example.com");
    private static readonly Artist Wegz = TrackTestData.Artist("Wegz", "wegz@example.com");

    private static readonly Track[] Data =
    [
        TrackTestData.Track(Amr, "beta", "EGAAA2600001", "Arabic Pop", "2026-01-01", TrackStatus.Submitted),
        TrackTestData.Track(Amr, "Alpha", "EGAAA2600002", "Arabic Pop", "2026-01-01"),
        TrackTestData.Track(Wegz, "Dorak Gai", "EGBBB2500001", "Rap", "2025-06-01", TrackStatus.Distributed),
        TrackTestData.Track(Wegz, "El Bakht", "EGBBB2700001", "Rap", "2027-03-01", TrackStatus.Submitted),
    ];

    private static List<TrackListItemDto> Page(TrackFilter filter, int page = 1, int size = 20) =>
        new TracksPageSpec(filter, page, size).Evaluate(Data).ToList();

    [Fact]
    public void PageSpec_OrdersByReleaseDateDescending_ThenTitleIgnoringCase() =>
        Page(TrackFilter.None).Select(t => t.Title).ShouldBe(["El Bakht", "Alpha", "beta", "Dorak Gai"]);

    [Fact]
    public void PageSpec_ProjectsArtistNameAndHyphenatedIsrc()
    {
        var item = Page(TrackFilter.None)[0];
        item.ArtistName.ShouldBe("Wegz");
        item.Isrc.ShouldBe("EG-BBB-27-00001");
        item.ReleaseDate.ShouldBe(new DateOnly(2027, 3, 1));
    }

    [Fact]
    public void PageSpec_FiltersCombineWithAnd()
    {
        Page(new TrackFilter(Status: TrackStatus.Submitted)).Select(t => t.Title).ShouldBe(["El Bakht", "beta"]);
        Page(new TrackFilter(Genre: "Rap")).Select(t => t.Title).ShouldBe(["El Bakht", "Dorak Gai"]);
        Page(new TrackFilter(ArtistId: Amr.Id)).Select(t => t.Title).ShouldBe(["Alpha", "beta"]);
        Page(new TrackFilter(Wegz.Id, "Rap", TrackStatus.Distributed)).Select(t => t.Title).ShouldBe(["Dorak Gai"]);
        Page(new TrackFilter(Amr.Id, "Rap")).ShouldBeEmpty();
        Page(new TrackFilter(ArtistId: Guid.NewGuid())).ShouldBeEmpty();
    }

    [Fact]
    public void CountSpec_UsesTheSameFilter()
    {
        var filter = new TrackFilter(Status: TrackStatus.Submitted);
        new TracksCountSpec(filter).Evaluate(Data).Count().ShouldBe(Page(filter).Count);
    }

    [Fact]
    public void PageSpec_SkipsAndTakes()
    {
        Page(TrackFilter.None, 2, 3).Select(t => t.Title).ShouldBe(["Dorak Gai"]);
        Page(TrackFilter.None, 3, 3).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("R&B", "submitted")]
    [InlineData("Hip-Hop", "distributed")]
    public void Validator_KnownOrEmptyFilters_Pass(string? genre, string? status) =>
        new ListTracksQueryValidator().Validate(new ListTracksQuery(1, 20, null, genre, status)).IsValid.ShouldBeTrue();

    [Theory]
    [InlineData("Pop", null, "Genre")]
    [InlineData(null, "published", "Status")]
    public void Validator_UnknownFilters_NameTheField(string? genre, string? status, string property) =>
        new ListTracksQueryValidator().Validate(new ListTracksQuery(1, 20, null, genre, status))
            .Errors.ShouldHaveSingleItem().PropertyName.ShouldBe(property);

    [Theory]
    [InlineData(0, 20, "Page")]
    [InlineData(1, 51, "PageSize")]
    public void Validator_Paging(int page, int size, string property) =>
        new ListTracksQueryValidator().Validate(new ListTracksQuery(page, size)).Errors.ShouldContain(e => e.PropertyName == property);

    [Fact]
    public async Task Handler_TranslatesFiltersAndReturnsTotals()
    {
        // Arrange
        var repository = Substitute.For<IReadRepository<Track>>();
        repository.CountAsync(Arg.Any<ISpecification<Track>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((ISpecification<Track>)ci[0]).Evaluate(Data).Count());
        repository.ListAsync(Arg.Any<ISpecification<Track, TrackListItemDto>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((ISpecification<Track, TrackListItemDto>)ci[0]).Evaluate(Data).ToList());

        // Act
        var result = await new ListTracksQueryHandler(repository)
            .Handle(new ListTracksQuery(1, 1, Wegz.Id, "", "submitted"), TestContext.Current.CancellationToken);

        // Assert
        result.Value.TotalCount.ShouldBe(1);
        result.Value.Items.ShouldHaveSingleItem().Title.ShouldBe("El Bakht");
    }

    [Fact]
    public void DetailSpec_HasAllowedNextStatusesAndNoDistributions()
    {
        var detail = new TrackDetailSpec(Data[0].Id).Evaluate(Data).Single();

        detail.Status.ShouldBe(TrackStatus.Submitted);
        detail.AllowedNextStatuses.ShouldBe([TrackStatus.Draft, TrackStatus.Distributed]);
        detail.Distributions.ShouldBeEmpty();
        detail.Isrc.ShouldBe("EG-AAA-26-00001");
        detail.ArtistName.ShouldBe("Amr Diab");
    }

    [Fact]
    public async Task GetTrack_Unknown_ReturnsNotFound()
    {
        var repository = Substitute.For<IReadRepository<Track>>();
        var result = await new GetTrackQueryHandler(repository).Handle(new GetTrackQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(Ardalis.Result.ResultStatus.NotFound);
    }
}
