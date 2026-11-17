using System.Net;
using TrackManagement.UnitTests.TestSupport;

namespace TrackManagement.UnitTests.Api.Tracks;

public sealed class ListTracksEndpointTests : TracksApiTestBase
{
    [Fact]
    public async Task List_DefaultPage_Returns20Of45WithTotals_NewestReleaseFirst()
    {
        // Arrange
        var artistId = await TracksApi.CreateArtistAsync(Client, AdminToken);
        for (var i = 1; i <= 45; i++)
        {
            await TracksApi.CreateTrackAsync(Client, AdminToken, artistId, $"EGAAA26{i:00000}", $"T{i:00}", releaseDate: $"2026-01-{(i % 28) + 1:00}");
        }

        // Act
        var response = await TracksApi.ListAsync(Client);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = await TracksApi.JsonAsync(response);
        var root = json.RootElement;
        root.GetProperty("items").GetArrayLength().ShouldBe(20);
        root.GetProperty("totalCount").GetInt32().ShouldBe(45);
        root.GetProperty("totalPages").GetInt32().ShouldBe(3);
        var dates = root.GetProperty("items").EnumerateArray().Select(t => t.GetProperty("releaseDate").GetString()).ToList();
        dates.ShouldBe(dates.OrderByDescending(d => d, StringComparer.Ordinal).ToList());
        var first = root.GetProperty("items")[0];
        first.GetProperty("artistName").GetString().ShouldBe("Amr Diab");
        first.GetProperty("isrc").GetString()!.ShouldMatch("^EG-AAA-26-[0-9]{5}$");

        // Page past the end: empty, totals still right
        using var past = await TracksApi.JsonAsync(await TracksApi.ListAsync(Client, "?page=99"));
        past.RootElement.GetProperty("items").GetArrayLength().ShouldBe(0);
        past.RootElement.GetProperty("totalCount").GetInt32().ShouldBe(45);
    }

    [Fact]
    public async Task List_FiltersCombine()
    {
        // Arrange
        var amr = await TracksApi.CreateArtistAsync(Client, AdminToken, "Amr Diab");
        var wegz = await TracksApi.CreateArtistAsync(Client, AdminToken, "Wegz");
        await TracksApi.CreateTrackAsync(Client, AdminToken, amr, "EGAAA2600001", "A1", "Arabic Pop");
        var submitted = await TracksApi.CreateTrackAsync(Client, AdminToken, wegz, "EGBBB2600001", "W1", "Rap");
        await TracksApi.CreateTrackAsync(Client, AdminToken, wegz, "EGBBB2600002", "W2", "R&B");
        (await TracksApi.ChangeStatusAsync(Client, AdminToken, submitted.ToString(), new { status = "submitted" })).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Act + Assert
        (await TitlesAsync("?status=submitted")).ShouldBe(["W1"]);
        (await TitlesAsync("?genre=R%26B")).ShouldBe(["W2"]);
        (await TitlesAsync($"?artistId={wegz}&genre=Rap&status=submitted")).ShouldBe(["W1"]);
        (await TitlesAsync($"?artistId={amr}&genre=Rap")).ShouldBeEmpty();
        (await TitlesAsync($"?artistId={Guid.NewGuid()}")).ShouldBeEmpty();
        (await TitlesAsync("?genre=&status=")).Count.ShouldBe(3);
    }

    [Theory]
    [InlineData("?status=published", "status")]
    [InlineData("?genre=Pop", "genre")]
    [InlineData("?pageSize=51", "pageSize")]
    public async Task List_UnknownFilterValues_Return400NamingTheField(string query, string field)
    {
        var response = await TracksApi.ListAsync(Client, query);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var json = await TracksApi.JsonAsync(response);
        json.RootElement.GetProperty("errors").TryGetProperty(field, out _).ShouldBeTrue();
    }

    [Fact]
    public async Task List_MalformedArtistId_Returns400()
    {
        var response = await TracksApi.ListAsync(Client, "?artistId=nope");
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    private async Task<List<string>> TitlesAsync(string query)
    {
        var response = await TracksApi.ListAsync(Client, query);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = await TracksApi.JsonAsync(response);
        return json.RootElement.GetProperty("items").EnumerateArray().Select(t => t.GetProperty("title").GetString()!).ToList();
    }
}

public sealed class GetTrackEndpointTests : TracksApiTestBase
{
    [Fact]
    public async Task Get_Anonymous_ReturnsFullDetail()
    {
        // Arrange
        var artistId = await TracksApi.CreateArtistAsync(Client, AdminToken, "Nancy عجرم");
        var id = await TracksApi.CreateTrackAsync(Client, AdminToken, artistId, "LBX2Y2612345", "Ya Tabtab", "Arabic Pop", "2026-12-01");

        // Act
        var response = await TracksApi.GetAsync(Client, id.ToString());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = await TracksApi.JsonAsync(response);
        var root = json.RootElement;
        root.GetProperty("title").GetString().ShouldBe("Ya Tabtab");
        root.GetProperty("artistName").GetString().ShouldBe("Nancy عجرم");
        root.GetProperty("isrc").GetString().ShouldBe("LB-X2Y-26-12345");
        root.GetProperty("status").GetString().ShouldBe("draft");
        root.GetProperty("createdAtUtc").GetDateTime().ShouldBe(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        root.GetProperty("updatedAtUtc").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        root.GetProperty("allowedNextStatuses").EnumerateArray().Select(s => s.GetString()).ShouldBe(["submitted"]);
        root.GetProperty("distributions").GetArrayLength().ShouldBe(0);
    }

    [Theory]
    [InlineData("0d1f6b9e-5c2a-4c7e-9f4b-2b8a7c1e9999")]
    [InlineData("not-a-guid")]
    public async Task Get_Unknown_Returns404(string id)
    {
        var response = await TracksApi.GetAsync(Client, id);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
