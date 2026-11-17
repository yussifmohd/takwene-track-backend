using System.Net;
using TrackManagement.UnitTests.Api.Artists;

namespace TrackManagement.UnitTests.Api.Dsps;

public sealed class ListDspsEndpointTests
{
    [Fact]
    public async Task List_Anonymous_ReturnsTheThreePlatformsSortedWithFixedIds()
    {
        // Arrange
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/dsps", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = await ArtistsApi.JsonAsync(response);
        var items = json.RootElement.EnumerateArray().Select(e => (Id: e.GetProperty("id").GetGuid(), Name: e.GetProperty("name").GetString())).ToList();
        items.Select(i => i.Name).ShouldBe(["Apple Music", "Spotify", "YouTube Music"]);
        items.Single(i => i.Name == "Spotify").Id.ShouldBe(new Guid("0d1f6b9e-5c2a-4c7e-9f4b-2b8a7c1e0001"));
    }
}
