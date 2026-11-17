using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace TrackManagement.UnitTests.Api.Auth;

/// <summary>Small HTTP helpers shared by the auth API tests.</summary>
internal static class AuthApi
{
    public const string LoginPath = "/api/auth/login";
    public const string MePath = "/api/auth/me";

    public static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password, string? clientIp = null, string? forwardedFor = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, LoginPath) { Content = JsonContent.Create(new { email, password }) };
        if (clientIp is not null)
        {
            request.Headers.Add(TestApiFactory.ClientIpHeader, clientIp);
        }

        if (forwardedFor is not null)
        {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static async Task<string> LoginAsAdminAsync(HttpClient client)
    {
        var response = await LoginAsync(client, TestApiFactory.AdminEmail, TestApiFactory.AdminPassword);
        response.EnsureSuccessStatusCode();
        using var json = await ReadJsonAsync(response);
        return json.RootElement.GetProperty("accessToken").GetString()!;
    }

    public static async Task<HttpResponseMessage> SendWithTokenAsync(HttpClient client, HttpMethod method, string path, string? token)
    {
        using var request = new HttpRequestMessage(method, path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
}
