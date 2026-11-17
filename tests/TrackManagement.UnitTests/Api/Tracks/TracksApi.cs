using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using TrackManagement.UnitTests.Api.Artists;
using TrackManagement.UnitTests.TestSupport;

namespace TrackManagement.UnitTests.Api.Tracks;

internal static class TracksApi
{
    public const string Path = "/api/tracks";

    public static Task<HttpResponseMessage> CreateAsync(HttpClient client, string? token, object body) =>
        SendAsync(client, HttpMethod.Post, Path, token, body);

    public static Task<HttpResponseMessage> ListAsync(HttpClient client, string query = "", string? token = null) =>
        SendAsync(client, HttpMethod.Get, Path + query, token);

    public static Task<HttpResponseMessage> GetAsync(HttpClient client, string id, string? token = null) =>
        SendAsync(client, HttpMethod.Get, $"{Path}/{id}", token);

    public static Task<HttpResponseMessage> ChangeStatusAsync(HttpClient client, string? token, string id, object body) =>
        SendAsync(client, HttpMethod.Patch, $"{Path}/{id}/status", token, body);

    /// <summary>Creates an artist through the API and returns its id.</summary>
    public static async Task<Guid> CreateArtistAsync(HttpClient client, string adminToken, string name = "Amr Diab")
    {
        var response = await ArtistsApi.CreateAsync(client, adminToken, new { name, countryCode = "EG", email = $"{Guid.NewGuid():N}@example.com" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var json = await JsonAsync(response);
        return json.RootElement.GetProperty("id").GetGuid();
    }

    /// <summary>Creates a track through the API (201 asserted) and returns its id.</summary>
    public static async Task<Guid> CreateTrackAsync(
        HttpClient client, string adminToken, Guid artistId, string isrc, string title = "Track", string genre = "Rap", string releaseDate = "2026-12-01")
    {
        var response = await CreateAsync(client, adminToken, new { title, artistId, isrc, genre, releaseDate });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using var json = await JsonAsync(response);
        return json.RootElement.GetProperty("id").GetGuid();
    }

    public static async Task<JsonDocument> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string? token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}

/// <summary>A fresh in-memory API per test class instance, with an admin token.</summary>
public abstract class TracksApiTestBase : IAsyncLifetime
{
    protected string AdminToken { get; } = TestTokens.Create(roles: ["Admin"]);

    protected TestApiFactory Factory { get; private set; } = null!;

    protected HttpClient Client { get; private set; } = null!;

    public ValueTask InitializeAsync()
    {
        Factory = new TestApiFactory();
        Client = Factory.CreateClient();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
