using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace TrackManagement.UnitTests.Api.Artists;

internal static class ArtistsApi
{
    public const string Path = "/api/artists";

    public static async Task<HttpResponseMessage> CreateAsync(HttpClient client, string? token, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Path) { Content = JsonContent.Create(body) };
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static async Task<HttpResponseMessage> ListAsync(HttpClient client, string query = "", string? token = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Path + query);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static async Task<JsonDocument> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
}
