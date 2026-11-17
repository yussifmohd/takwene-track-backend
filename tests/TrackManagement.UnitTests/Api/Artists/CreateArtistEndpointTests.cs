using System.Net;
using TrackManagement.UnitTests.TestSupport;

namespace TrackManagement.UnitTests.Api.Artists;

public sealed class CreateArtistEndpointTests : IAsyncLifetime
{
    private readonly string _adminToken = TestTokens.Create(roles: ["Admin"]);
    private TestApiFactory _factory = null!;
    private HttpClient _client = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new TestApiFactory();
        _client = _factory.CreateClient();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Create_AsAdmin_Returns201WithLocationAndNormalizedArtist()
    {
        // Act
        var response = await ArtistsApi.CreateAsync(_client, _adminToken, new { name = " عمرو دياب ", countryCode = " eg ", email = " Booking@Example.com " });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var json = await ArtistsApi.JsonAsync(response);
        var root = json.RootElement;
        var id = root.GetProperty("id").GetGuid();
        response.Headers.Location!.ToString().ShouldBe($"/api/artists/{id}");
        root.GetProperty("name").GetString().ShouldBe("عمرو دياب");
        root.GetProperty("countryCode").GetString().ShouldBe("EG");
        root.GetProperty("email").GetString().ShouldBe("Booking@Example.com");
        root.GetProperty("createdAtUtc").GetDateTime().ShouldBe(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Create_WithoutToken_Returns401()
    {
        var response = await ArtistsApi.CreateAsync(_client, null, new { name = "A", countryCode = "EG", email = "a@b.eg" });
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_WithNonAdminToken_Returns403()
    {
        var response = await ArtistsApi.CreateAsync(_client, TestTokens.Create(roles: []), new { name = "A", countryCode = "EG", email = "a@b.eg" });
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_MissingFields_Returns400NamingEachField()
    {
        // Act
        var response = await ArtistsApi.CreateAsync(_client, _adminToken, new { name = "", countryCode = "XX" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var json = await ArtistsApi.JsonAsync(response);
        var errors = json.RootElement.GetProperty("errors");
        errors.GetProperty("name")[0].GetString().ShouldBe("Name is required.");
        errors.GetProperty("countryCode")[0].GetString().ShouldBe("Country must be an ISO 3166-1 alpha-2 code, for example EG or SA.");
        errors.GetProperty("email")[0].GetString().ShouldBe("Email is required.");
    }

    [Fact]
    public async Task Create_DuplicateEmailIgnoringCaseAndSpaces_Returns409WithEmailError()
    {
        // Arrange
        (await ArtistsApi.CreateAsync(_client, _adminToken, new { name = "One", countryCode = "EG", email = "booking@example.com" }))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        // Act
        var response = await ArtistsApi.CreateAsync(_client, _adminToken, new { name = "Two", countryCode = "SA", email = "  BOOKING@example.com " });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        using var json = await ArtistsApi.JsonAsync(response);
        json.RootElement.GetProperty("title").GetString().ShouldBe("Conflict");
        json.RootElement.GetProperty("errors").GetProperty("email")[0].GetString().ShouldBe("An artist with this email already exists.");
    }

    [Fact]
    public async Task Create_EmojiNameAt200CodePoints_Accepted()
    {
        var name = string.Concat(Enumerable.Repeat("🎤", 200));
        var response = await ArtistsApi.CreateAsync(_client, _adminToken, new { name, countryCode = "AE", email = "mic@example.com" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }
}
