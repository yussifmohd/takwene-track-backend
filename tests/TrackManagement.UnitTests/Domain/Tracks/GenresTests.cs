using TrackManagement.Domain.Tracks;

namespace TrackManagement.UnitTests.Domain.Tracks;

public sealed class GenresTests
{
    [Fact]
    public void All_IsTheFixedList() =>
        Genres.All.ShouldBe(["Arabic Pop", "Mahraganat", "Khaleeji", "Hip-Hop", "Electronic", "Rap", "R&B", "Rock"]);

    [Fact]
    public void All_FitTheColumn() => Genres.All.ShouldAllBe(g => g.Length <= Genres.MaxLength);

    [Theory]
    [InlineData("Arabic Pop")]
    [InlineData("R&B")]
    [InlineData("Hip-Hop")]
    public void IsKnown_ExactNames(string name) => Genres.IsKnown(name).ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("r&b")]
    [InlineData("HipHop")]
    [InlineData("Pop")]
    [InlineData(" Rap")]
    public void IsKnown_OtherSpellings_AreRefused(string? name) => Genres.IsKnown(name).ShouldBeFalse();
}
