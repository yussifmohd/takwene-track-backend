using System.Net;

namespace TrackManagement.UnitTests.Api;

public sealed class OpenApiExposureTests
{
    [Fact]
    public async Task Development_ServesOpenApiWithBearerSchemeAndHealthOperation()
    {
        // Arrange
        await using var factory = new TestApiFactory("Development");
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("\"Bearer\"");
        body.ShouldContain("\"/api/health\"");
        body.ShouldContain("\"GetHealth\"");
    }

    [Fact]
    public async Task Development_DocumentContainsOnlyProductionRoutes()
    {
        // Guards the contract: test-only endpoints from TestApiFactory must never be described.
        await using var factory = new TestApiFactory("Development");
        using var client = factory.CreateClient();

        using var json = System.Text.Json.JsonDocument.Parse(
            await client.GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken));

        json.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name)
            .ShouldAllBe(path => path.StartsWith("/api/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Development_ServesScalarReference()
    {
        // Arrange
        await using var factory = new TestApiFactory("Development");
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/scalar", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar")]
    public async Task Production_DoesNotExposeDocs(string path)
    {
        // Arrange
        await using var factory = new TestApiFactory("Production");
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
