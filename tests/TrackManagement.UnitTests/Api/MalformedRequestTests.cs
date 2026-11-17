using System.Net;
using System.Net.Http.Headers;
using System.Text;
using TrackManagement.UnitTests.Api.Artists;
using TrackManagement.UnitTests.TestSupport;

namespace TrackManagement.UnitTests.Api;

/// <summary>
/// Client mistakes are 400 ProblemDetails in every environment. Before Phase 2, Development (where
/// Minimal APIs throw BadHttpRequestException) answered them with 500.
/// </summary>
public sealed class MalformedRequestTests
{
    public static TheoryData<string> Environments => ["Development", "Testing", "Production"];

    private static async Task<HttpResponseMessage> PostRawAsync(HttpClient client, string path, string body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(roles: ["Admin"]));
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    [Theory]
    [MemberData(nameof(Environments))]
    public async Task InvalidJson_Returns400ProblemDetails(string environment)
    {
        await using var factory = new TestApiFactory(environment);
        using var client = factory.CreateClient();

        var response = await PostRawAsync(client, ArtistsApi.Path, "{not json");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Theory]
    [MemberData(nameof(Environments))]
    public async Task WrongJsonType_Returns400ProblemDetails(string environment)
    {
        await using var factory = new TestApiFactory(environment);
        using var client = factory.CreateClient();

        var response = await PostRawAsync(client, ArtistsApi.Path, "{\"name\":42,\"countryCode\":\"EG\",\"email\":\"a@b.eg\"}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Theory]
    [MemberData(nameof(Environments))]
    public async Task NonNumericPaging_Returns400ProblemDetails(string environment)
    {
        await using var factory = new TestApiFactory(environment);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/artists?page=abc", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task InvalidJson_InDevelopment_RequestLogShowsTheStatusTheClientGot()
    {
        await using var factory = new TestApiFactory("Development");
        using var client = factory.CreateClient();

        await PostRawAsync(client, ArtistsApi.Path, "{not json");

        // The request log is written once the pipeline unwinds, which can be after the client has the response.
        string? requestLog = null;
        for (var attempt = 0; attempt < 50 && requestLog is null; attempt++)
        {
            requestLog = factory.Logs.Lines.SingleOrDefault(l => l.StartsWith("HTTP \"POST\" \"/api/artists\"", StringComparison.Ordinal));
            if (requestLog is null)
            {
                await Task.Delay(20, TestContext.Current.CancellationToken);
            }
        }

        requestLog.ShouldNotBeNull();
        requestLog.ShouldContain("responded 400");
    }

    [Fact]
    public async Task InvalidJson_InDevelopment_DoesNotLeakParserDetails()
    {
        await using var factory = new TestApiFactory("Development");
        using var client = factory.CreateClient();

        var response = await PostRawAsync(client, ArtistsApi.Path, "{not json");

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldNotContain("JsonException");
        body.ShouldNotContain("   at ");
    }
}
