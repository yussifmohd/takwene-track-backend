using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TrackManagement.Api.Security;

namespace TrackManagement.UnitTests.Api.Security;

public sealed class CorsTests
{
    private const string WebApp = "http://localhost:8081";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static TestApiFactory Create(params string[] origins) => new(settings:
        origins.Select((o, i) => (Key: $"Cors:AllowedOrigins:{i}", Value: (string?)o)).ToDictionary(p => p.Key, p => p.Value));

    private static async Task<HttpResponseMessage> PreflightAsync(HttpClient client, string origin, string method)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/tracks");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        return await client.SendAsync(request, Ct);
    }

    [Fact]
    public async Task ListedOrigin_IsAllowed_WithTheMethodsTheWebAppUses()
    {
        await using var factory = Create(WebApp);
        using var client = factory.CreateClient();

        var response = await PreflightAsync(client, WebApp, "PATCH");

        response.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe([WebApp]);
        response.Headers.GetValues("Access-Control-Allow-Methods").Single().Split(',').Select(m => m.Trim())
            .ShouldBe(WebAppCors.Methods, ignoreOrder: true);
    }

    [Fact]
    public async Task OtherOrigin_GetsNoCorsHeaders()
    {
        await using var factory = Create(WebApp);
        using var client = factory.CreateClient();

        var preflight = await PreflightAsync(client, "https://evil.example", "POST");
        using var get = new HttpRequestMessage(HttpMethod.Get, "/api/dsps");
        get.Headers.Add("Origin", "https://evil.example");
        var simple = await client.SendAsync(get, Ct);

        preflight.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
        simple.StatusCode.ShouldBe(HttpStatusCode.OK);
        simple.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    [Fact]
    public async Task UnusedMethod_IsNotAllowed()
    {
        await using var factory = Create(WebApp);
        using var client = factory.CreateClient();

        var response = await PreflightAsync(client, WebApp, "DELETE");

        // ASP.NET Core answers with the allowed list; the browser then refuses DELETE.
        response.Headers.GetValues("Access-Control-Allow-Methods").Single().ShouldNotContain("DELETE");
    }

    [Theory]
    [InlineData("*")]
    [InlineData("https://*.example.com")]
    [InlineData("localhost:8081")]
    [InlineData("http://localhost:8081/app")]
    [InlineData("http://localhost:8081/")]
    [InlineData("ftp://files.example.com")]
    [InlineData("")]
    public void InvalidOrigins_AreRejected(string origin) => WebAppCors.IsOrigin(origin).ShouldBeFalse();

    [Theory]
    [InlineData("http://localhost:8081")]
    [InlineData("https://tracks.takwene.example")]
    public void ExactOrigins_AreAccepted(string origin) => WebAppCors.IsOrigin(origin).ShouldBeTrue();

    [Fact]
    public void WildcardOrigin_FailsValidation_SoTheApiDoesNotStart()
    {
        using var services = ApiOptions.Build(new() { ["Cors:AllowedOrigins:0"] = "*" });

        var ex = Should.Throw<OptionsValidationException>(() => services.GetRequiredService<IOptions<WebAppCorsOptions>>().Value);

        ex.Message.ShouldContain("Cors:AllowedOrigins");
    }
}
