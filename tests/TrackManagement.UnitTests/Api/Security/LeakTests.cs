using System.Net;
using TrackManagement.UnitTests.Api.Artists;
using TrackManagement.UnitTests.Api.Auth;
using TrackManagement.UnitTests.TestSupport;

namespace TrackManagement.UnitTests.Api.Security;

/// <summary>
/// Errors answer with ProblemDetails and nothing internal; logs never contain secrets.
/// (404, 500, malformed JSON and 413 are covered in ProblemDetailsTests, MalformedRequestTests and RequestBodyLimitTests.)
/// </summary>
public sealed class LeakTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task WrongMethod_Returns405ProblemWithoutInternals()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/dsps", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var body = await response.Content.ReadAsStringAsync(Ct);
        body.ShouldNotContain("Exception");
        body.ShouldNotContain("   at ");
    }

    [Fact]
    public async Task WrongContentType_Returns415ProblemWithoutInternals()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();
        var token = await AuthApi.LoginAsAdminAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/artists") { Content = new StringContent("<artist/>", System.Text.Encoding.UTF8, "application/xml") };
        request.Headers.Authorization = new("Bearer", token);

        var response = await client.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
        var body = await response.Content.ReadAsStringAsync(Ct);
        body.ShouldNotContain("Exception");
        body.ShouldNotContain("   at ");
    }

    [Fact]
    public async Task SignInAndChanges_NeverLogPasswordsTokensKeysOrConnectionStrings()
    {
        // Arrange
        await using var factory = new TestApiFactory("Development");
        using var client = factory.CreateClient();

        // Act
        await AuthApi.LoginAsync(client, TestApiFactory.AdminEmail, "Wrong!Passw0rd#1");
        var token = await AuthApi.LoginAsAdminAsync(client);
        await ArtistsApi.CreateAsync(client, token, new { name = "Wegz", countryCode = "EG", email = "wegz@example.com" });
        await AuthApi.SendWithTokenAsync(client, HttpMethod.Get, AuthApi.MePath, token);

        // Assert
        var log = string.Join('\n', factory.Logs.Lines);
        log.ShouldNotBeEmpty();
        log.ShouldNotContain(TestApiFactory.AdminPassword);
        log.ShouldNotContain("Wrong!Passw0rd#1");
        log.ShouldNotContain(token);
        log.ShouldNotContain(token.Split('.')[2], Case.Sensitive, "not even the signature part");
        log.ShouldNotContain(TestTokens.SigningKey);
        log.ShouldNotContain("Host=unused-in-tests");
        log.ShouldNotContain(TestApiFactory.AdminEmail, Case.Insensitive, "emails are masked in sign-in logs");
    }
}
