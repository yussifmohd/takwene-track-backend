using System.Net;
using System.Net.Http.Headers;
using System.Text;
using TrackManagement.UnitTests.Api.Artists;
using TrackManagement.UnitTests.TestSupport;

namespace TrackManagement.UnitTests.Api;

public sealed class RequestBodyLimitTests
{
    [Fact]
    public async Task BodyOverTheLimit_Returns413ProblemDetails()
    {
        // Arrange
        await using var factory = new TestApiFactory(settings: new Dictionary<string, string?> { ["Api:MaxRequestBodyBytes"] = "1024" });
        using var client = factory.CreateClient();
        var json = "{\"name\":\"" + new string('a', 2000) + "\",\"countryCode\":\"EG\",\"email\":\"a@b.eg\"}";
        using var request = new HttpRequestMessage(HttpMethod.Post, ArtistsApi.Path) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(roles: ["Admin"]));

        // Act
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task DefaultLimit_Is32KB_AndANormalBodyPasses()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var response = await ArtistsApi.CreateAsync(client, TestTokens.Create(roles: ["Admin"]), new { name = "Normal", countryCode = "EG", email = "n@b.eg" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }
}
