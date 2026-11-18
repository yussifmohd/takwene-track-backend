using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TrackManagement.Api.Security;
using TrackManagement.UnitTests.Api.Artists;
using TrackManagement.UnitTests.Api.Auth;
using TrackManagement.UnitTests.TestSupport;

namespace TrackManagement.UnitTests.Api.Security;

/// <summary>Limits beyond sign-in: all requests per IP, and change requests per user.</summary>
public sealed class RateLimitTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<HttpResponseMessage> GetFromAsync(HttpClient client, string path, string ip)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(TestApiFactory.ClientIpHeader, ip);
        return await client.SendAsync(request, Ct);
    }

    [Fact]
    public async Task OverTheGlobalLimit_Returns429ProblemWithRetryAfter()
    {
        // Arrange
        await using var factory = new TestApiFactory(settings: new Dictionary<string, string?> { ["RateLimiting:Api:PermitLimit"] = "3" });
        using var client = factory.CreateClient();
        for (var i = 0; i < 3; i++)
        {
            (await GetFromAsync(client, "/api/dsps", "198.51.100.20")).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        // Act
        var response = await GetFromAsync(client, "/api/tracks", "198.51.100.20");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        response.Headers.RetryAfter.ShouldNotBeNull();
        using var json = await AuthApi.ReadJsonAsync(response);
        json.RootElement.GetProperty("detail").GetString()!.ShouldStartWith("Too many requests.");
    }

    [Fact]
    public async Task GlobalLimit_IsPerClientIp_AndHealthChecksAreNotCounted()
    {
        // Arrange
        await using var factory = new TestApiFactory(settings: new Dictionary<string, string?> { ["RateLimiting:Api:PermitLimit"] = "2" });
        using var client = factory.CreateClient();
        for (var i = 0; i < 3; i++)
        {
            await GetFromAsync(client, "/api/dsps", "198.51.100.21");
        }

        // Act + Assert
        (await GetFromAsync(client, "/api/dsps", "198.51.100.22")).StatusCode.ShouldBe(HttpStatusCode.OK);
        for (var i = 0; i < 5; i++)
        {
            (await GetFromAsync(client, "/api/health", "198.51.100.21")).StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
            (await GetFromAsync(client, "/health", "198.51.100.21")).StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        }
    }

    [Fact]
    public async Task OverTheWriteLimit_Returns429ForThatUser_AndAnotherUserIsUnaffected()
    {
        // Arrange
        await using var factory = new TestApiFactory(settings: new Dictionary<string, string?> { ["RateLimiting:Writes:PermitLimit"] = "2" });
        using var client = factory.CreateClient();
        var first = TestTokens.Create();
        var second = TestTokens.Create();
        object Body() => new { name = "Wegz", countryCode = "EG", email = $"{Guid.NewGuid():N}@example.com" };
        (await ArtistsApi.CreateAsync(client, first, Body())).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await ArtistsApi.CreateAsync(client, first, Body())).StatusCode.ShouldBe(HttpStatusCode.Created);

        // Act
        var limited = await ArtistsApi.CreateAsync(client, first, Body());
        var other = await ArtistsApi.CreateAsync(client, second, Body());

        // Assert
        limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        limited.Headers.RetryAfter.ShouldNotBeNull();
        other.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await ArtistsApi.ListAsync(client)).StatusCode.ShouldBe(HttpStatusCode.OK, "reads are not write-limited");
    }

    [Theory]
    [InlineData("RateLimiting:Api:PermitLimit")]
    [InlineData("RateLimiting:Writes:WindowSeconds")]
    public void NonPositiveSetting_FailsValidation_SoTheApiDoesNotStart(string key)
    {
        using var services = ApiOptions.Build(new() { [key] = "0" });

        var ex = Should.Throw<OptionsValidationException>(() =>
        {
            _ = services.GetRequiredService<IOptions<ApiRateLimitOptions>>().Value;
            _ = services.GetRequiredService<IOptions<WritesRateLimitOptions>>().Value;
        });

        ex.Message.ShouldContain(key[..key.LastIndexOf(':')]);
    }
}
