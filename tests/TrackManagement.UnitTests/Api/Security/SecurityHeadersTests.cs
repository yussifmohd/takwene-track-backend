using TrackManagement.Api.Security;

namespace TrackManagement.UnitTests.Api.Security;

public sealed class SecurityHeadersTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/api/dsps")]
    [InlineData("/api/tracks/not-a-route")]
    [InlineData("/api/health")]
    [InlineData("/health")]
    [InlineData("/nothing-here")]
    public async Task EveryResponse_CarriesTheSecurityHeaders(string path)
    {
        // Arrange
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync(path, Ct);

        // Assert
        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        response.Headers.GetValues("X-Frame-Options").ShouldBe(["DENY"]);
        response.Headers.GetValues("Referrer-Policy").ShouldBe(["no-referrer"]);
        response.Headers.GetValues("Content-Security-Policy").ShouldBe([SecurityHeaders.ContentSecurityPolicy]);
    }

    [Fact]
    public async Task ErrorResponses_CarryTheHeadersToo()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(TestApiFactory.ThrowingPath, Ct);

        ((int)response.StatusCode).ShouldBe(500);
        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
    }

    [Fact]
    public async Task ApiResponses_AreNeverStored()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dsps", Ct);

        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
    }

    [Fact]
    public async Task DevelopmentDocs_HaveNoContentPolicy_SoTheyStillLoad()
    {
        await using var factory = new TestApiFactory("Development");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/scalar", Ct);

        response.IsSuccessStatusCode.ShouldBeTrue();
        response.Headers.Contains("Content-Security-Policy").ShouldBeFalse();
        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
    }
}
