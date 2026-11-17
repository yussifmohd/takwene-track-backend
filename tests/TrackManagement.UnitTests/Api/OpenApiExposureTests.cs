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
