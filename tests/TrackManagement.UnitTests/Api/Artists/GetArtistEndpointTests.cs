using System.Net;
using TrackManagement.UnitTests.Api.Tracks;

namespace TrackManagement.UnitTests.Api.Artists;

public sealed class GetArtistEndpointTests : TracksApiTestBase
{
    [Fact]
    public async Task Get_ByLocationOfCreate_EmailOnlyForAdmins()
    {
        // Arrange: the Location header from Phase 2's create now resolves
        var created = await ArtistsApi.CreateAsync(Client, AdminToken, new { name = "فيروز", countryCode = "LB", email = "fairuz@example.com" });
        var location = created.Headers.Location!.ToString();

        // Act
        using var visitor = new HttpRequestMessage(HttpMethod.Get, location);
        var anonymous = await Client.SendAsync(visitor, TestContext.Current.CancellationToken);
        using var admin = new HttpRequestMessage(HttpMethod.Get, location);
        admin.Headers.Authorization = new("Bearer", AdminToken);
        var asAdmin = await Client.SendAsync(admin, TestContext.Current.CancellationToken);

        // Assert
        anonymous.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var anonymousJson = await ArtistsApi.JsonAsync(anonymous);
        anonymousJson.RootElement.GetProperty("name").GetString().ShouldBe("فيروز");
        anonymousJson.RootElement.TryGetProperty("email", out _).ShouldBeFalse();
        using var adminJson = await ArtistsApi.JsonAsync(asAdmin);
        adminJson.RootElement.GetProperty("email").GetString().ShouldBe("fairuz@example.com");
    }

    [Fact]
    public async Task Get_Unknown_Returns404()
    {
        var response = await ArtistsApi.ListAsync(Client, $"/{Guid.NewGuid()}");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_Search_FindsByPartOfTheName_InAnyScript()
    {
        // Arrange
        await TracksApi.CreateArtistAsync(Client, AdminToken, "Amr Diab");
        await TracksApi.CreateArtistAsync(Client, AdminToken, "عمرو دياب");
        await TracksApi.CreateArtistAsync(Client, AdminToken, "Tamer Hosny");

        // Act + Assert
        (await NamesAsync("?search=AMR")).ShouldBe(["Amr Diab"]);
        (await NamesAsync("?search=%D8%B9%D9%85%D8%B1%D9%88")).ShouldBe(["عمرو دياب"]);
        (await NamesAsync("?search=")).Count.ShouldBe(3);
        (await ArtistsApi.ListAsync(Client, $"?search={new string('a', 101)}")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task<List<string>> NamesAsync(string query)
    {
        using var json = await ArtistsApi.JsonAsync(await ArtistsApi.ListAsync(Client, query));
        var root = json.RootElement;
        var names = root.GetProperty("items").EnumerateArray().Select(a => a.GetProperty("name").GetString()!).ToList();
        root.GetProperty("totalCount").GetInt32().ShouldBe(names.Count);
        return names;
    }
}
