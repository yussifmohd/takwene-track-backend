using System.Net;

namespace TrackManagement.UnitTests.Api.Auth;

public sealed class LoginRateLimitTests
{
    private static TestApiFactory Create(params (string Key, string Value)[] extra)
    {
        var settings = new Dictionary<string, string?>
        {
            ["RateLimiting:Login:PermitLimit"] = "3",
            ["RateLimiting:Login:WindowSeconds"] = "60",
        };
        foreach (var (key, value) in extra)
        {
            settings[key] = value;
        }

        return new TestApiFactory(settings: settings);
    }

    [Fact]
    public async Task OverLimit_Returns429ProblemWithRetryAfter_EvenWithValidCredentials()
    {
        // Arrange
        await using var factory = Create();
        using var client = factory.CreateClient();
        for (var i = 0; i < 3; i++)
        {
            (await AuthApi.LoginAsync(client, TestApiFactory.AdminEmail, TestApiFactory.AdminPassword, clientIp: "198.51.100.7"))
                .StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        // Act
        var response = await AuthApi.LoginAsync(client, TestApiFactory.AdminEmail, TestApiFactory.AdminPassword, clientIp: "198.51.100.7");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        response.Headers.RetryAfter.ShouldNotBeNull();
        response.Headers.RetryAfter!.Delta!.Value.ShouldBeInRange(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60));
        using var json = await AuthApi.ReadJsonAsync(response);
        json.RootElement.GetProperty("status").GetInt32().ShouldBe(429);
    }

    [Fact]
    public async Task Limit_IsPerClientIp()
    {
        await using var factory = Create();
        using var client = factory.CreateClient();
        for (var i = 0; i < 4; i++)
        {
            await AuthApi.LoginAsync(client, TestApiFactory.AdminEmail, "Wrong!Passw0rd#1", clientIp: "198.51.100.7");
        }

        var other = await AuthApi.LoginAsync(client, TestApiFactory.AdminEmail, TestApiFactory.AdminPassword, clientIp: "198.51.100.8");

        other.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OtherEndpoints_AreNotLimited()
    {
        await using var factory = Create();
        using var client = factory.CreateClient();

        for (var i = 0; i < 6; i++)
        {
            (await client.GetAsync("/api/health", TestContext.Current.CancellationToken)).StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        }
    }

    [Fact]
    public async Task TrustedProxy_ForwardedClientIpsAreLimitedSeparately()
    {
        // Arrange: the proxy range comes from configuration (here a test VPC range).
        await using var factory = Create(("ReverseProxy:KnownNetworks:0", "10.20.0.0/16"));
        using var client = factory.CreateClient();
        for (var i = 0; i < 3; i++)
        {
            await AuthApi.LoginAsync(client, TestApiFactory.AdminEmail, TestApiFactory.AdminPassword, clientIp: "10.20.1.5", forwardedFor: "203.0.113.1");
        }

        // Act: same proxy, different real client.
        var response = await AuthApi.LoginAsync(client, TestApiFactory.AdminEmail, TestApiFactory.AdminPassword, clientIp: "10.20.1.5", forwardedFor: "203.0.113.2");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UntrustedSource_CannotDodgeTheLimitBySpoofingForwardedFor()
    {
        await using var factory = Create(("ReverseProxy:KnownNetworks:0", "10.20.0.0/16"));
        using var client = factory.CreateClient();
        for (var i = 0; i < 3; i++)
        {
            await AuthApi.LoginAsync(client, TestApiFactory.AdminEmail, TestApiFactory.AdminPassword, clientIp: "198.51.100.7", forwardedFor: $"203.0.113.{i + 10}");
        }

        var response = await AuthApi.LoginAsync(client, TestApiFactory.AdminEmail, TestApiFactory.AdminPassword, clientIp: "198.51.100.7", forwardedFor: "203.0.113.99");

        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }
}
