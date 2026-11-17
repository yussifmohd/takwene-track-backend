using System.Net;
using TrackManagement.UnitTests.TestSupport;

namespace TrackManagement.UnitTests.Api.Artists;

public sealed class ListArtistsEndpointTests : IAsyncLifetime
{
    private readonly string _adminToken = TestTokens.Create(roles: ["Admin"]);
    private TestApiFactory _factory = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new TestApiFactory();
        _client = _factory.CreateClient();
        // 23 artists: "Artist 01".."Artist 22" plus one Arabic name (sorts last).
        for (var i = 1; i <= 22; i++)
        {
            (await ArtistsApi.CreateAsync(_client, _adminToken, new { name = $"Artist {i:00}", countryCode = "EG", email = $"a{i}@example.com" }))
                .StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        (await ArtistsApi.CreateAsync(_client, _adminToken, new { name = "عمرو دياب", countryCode = "EG", email = "amr@example.com" }))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task List_Anonymous_DefaultsTo20PerPage_WithoutEmails()
    {
        // Act
        var response = await ArtistsApi.ListAsync(_client);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = await ArtistsApi.JsonAsync(response);
        var root = json.RootElement;
        root.GetProperty("page").GetInt32().ShouldBe(1);
        root.GetProperty("pageSize").GetInt32().ShouldBe(20);
        root.GetProperty("totalCount").GetInt32().ShouldBe(23);
        root.GetProperty("totalPages").GetInt32().ShouldBe(2);
        var items = root.GetProperty("items").EnumerateArray().ToList();
        items.Count.ShouldBe(20);
        items[0].GetProperty("name").GetString().ShouldBe("Artist 01");
        items.Any(i => i.TryGetProperty("email", out _)).ShouldBeFalse();
    }

    [Fact]
    public async Task List_AsAdmin_IncludesEmails()
    {
        var response = await ArtistsApi.ListAsync(_client, "?pageSize=50", _adminToken);

        using var json = await ArtistsApi.JsonAsync(response);
        var items = json.RootElement.GetProperty("items").EnumerateArray().ToList();
        items.Count.ShouldBe(23);
        items.ShouldAllBe(i => i.GetProperty("email").GetString()!.Contains('@'));
        items[^1].GetProperty("name").GetString().ShouldBe("عمرو دياب");
    }

    [Fact]
    public async Task List_NonAdminToken_HidesEmails()
    {
        var response = await ArtistsApi.ListAsync(_client, "", TestTokens.Create(roles: []));

        using var json = await ArtistsApi.JsonAsync(response);
        json.RootElement.GetProperty("items").EnumerateArray().Any(i => i.TryGetProperty("email", out _)).ShouldBeFalse();
    }

    [Fact]
    public async Task List_SecondPage_HasTheRest()
    {
        using var json = await ArtistsApi.JsonAsync(await ArtistsApi.ListAsync(_client, "?page=2"));
        json.RootElement.GetProperty("items").GetArrayLength().ShouldBe(3);
    }

    [Fact]
    public async Task List_PagePastTheEnd_IsEmptyWithRealTotals()
    {
        var response = await ArtistsApi.ListAsync(_client, "?page=9");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = await ArtistsApi.JsonAsync(response);
        json.RootElement.GetProperty("items").GetArrayLength().ShouldBe(0);
        json.RootElement.GetProperty("totalCount").GetInt32().ShouldBe(23);
    }

    [Theory]
    [InlineData("?page=0", "page")]
    [InlineData("?pageSize=0", "pageSize")]
    [InlineData("?pageSize=51", "pageSize")]
    public async Task List_OutOfRangePaging_Returns400NamingTheValue(string query, string field)
    {
        var response = await ArtistsApi.ListAsync(_client, query);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var json = await ArtistsApi.JsonAsync(response);
        json.RootElement.GetProperty("errors").TryGetProperty(field, out _).ShouldBeTrue();
    }
}
