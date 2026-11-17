using System.Net;
using Microsoft.IdentityModel.Tokens;
using TrackManagement.UnitTests.TestSupport;

namespace TrackManagement.UnitTests.Api.Auth;

/// <summary>Token validation and the Admin policy, through the real pipeline (test-only endpoints from TestApiFactory).</summary>
public sealed class AuthorizationTests : IAsyncLifetime
{
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
    public async Task AdminToken_FromLogin_PassesAdminPolicy()
    {
        var token = await AuthApi.LoginAsAdminAsync(_client);

        var response = await AuthApi.SendWithTokenAsync(_client, HttpMethod.Post, TestApiFactory.AdminOnlyPath, token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NoToken_OnAdminEndpoint_Returns401Problem()
    {
        var response = await AuthApi.SendWithTokenAsync(_client, HttpMethod.Post, TestApiFactory.AdminOnlyPath, null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task ValidTokenWithoutAdminRole_Returns403Problem()
    {
        var token = TestTokens.Create(roles: ["Viewer"]);

        var response = await AuthApi.SendWithTokenAsync(_client, HttpMethod.Post, TestApiFactory.AdminOnlyPath, token);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task PublicEndpoint_NeedsNoToken() =>
        (await AuthApi.SendWithTokenAsync(_client, HttpMethod.Get, TestApiFactory.PublicPath, null)).StatusCode.ShouldBe(HttpStatusCode.OK);

    public static TheoryData<string, string> RejectedTokens => new()
    {
        { "expired beyond clock skew", TestTokens.Create(notBefore: DateTime.UtcNow.AddMinutes(-70), expires: DateTime.UtcNow.AddMinutes(-2)) },
        { "not yet valid", TestTokens.Create(notBefore: DateTime.UtcNow.AddMinutes(5), expires: DateTime.UtcNow.AddMinutes(65)) },
        { "wrong issuer", TestTokens.Create(issuer: "someone-else") },
        { "wrong audience", TestTokens.Create(audience: "another-app") },
        { "wrong signing key", TestTokens.Create(signingKey: "another-signing-key-0123456789-abcdefghijklmnopqrstuvwxyz-XYZ") },
        { "HS384 instead of HS256", TestTokens.Create(algorithm: SecurityAlgorithms.HmacSha384) },
        { "HS512 instead of HS256", TestTokens.Create(algorithm: SecurityAlgorithms.HmacSha512) },
        { "unsigned (alg none)", TestTokens.CreateUnsigned() },
        { "payload tampered", TestTokens.Tamper(TestTokens.Create()) },
        { "garbage", "not.a.jwt" },
    };

    [Theory]
    [MemberData(nameof(RejectedTokens))]
    public async Task InvalidToken_IsRejectedWith401(string reason, string token)
    {
        var response = await AuthApi.SendWithTokenAsync(_client, HttpMethod.Post, TestApiFactory.AdminOnlyPath, token);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, reason);
    }

    [Fact]
    public async Task TokenExpiredWithinClockSkew_IsStillAccepted()
    {
        var token = TestTokens.Create(notBefore: DateTime.UtcNow.AddMinutes(-60), expires: DateTime.UtcNow.AddSeconds(-10));

        var response = await AuthApi.SendWithTokenAsync(_client, HttpMethod.Post, TestApiFactory.AdminOnlyPath, token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Me_WithAdminToken_ReturnsEmailRolesAndExpiry()
    {
        var token = await AuthApi.LoginAsAdminAsync(_client);

        var response = await AuthApi.SendWithTokenAsync(_client, HttpMethod.Get, AuthApi.MePath, token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = await AuthApi.ReadJsonAsync(response);
        json.RootElement.GetProperty("email").GetString().ShouldBe(TestApiFactory.AdminEmail);
        json.RootElement.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ShouldBe(["Admin"]);
        json.RootElement.GetProperty("expiresAt").GetDateTimeOffset().ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(59));
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401() =>
        (await AuthApi.SendWithTokenAsync(_client, HttpMethod.Get, AuthApi.MePath, null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
}
