using System.Net;
using System.Text.Json;

namespace TrackManagement.UnitTests.Api;

public sealed class ProblemDetailsTests
{
    [Fact]
    public async Task UnknownRoute_Returns404ProblemDetailsWithTraceId()
    {
        // Arrange
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/does-not-exist", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        json.RootElement.GetProperty("status").GetInt32().ShouldBe(404);
        json.RootElement.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UnhandledException_Returns500GenericProblemWithoutInternals()
    {
        // Arrange
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync(TestApiFactory.ThrowingPath, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldNotContain(TestApiFactory.SecretExceptionMessage);
        body.ShouldNotContain("InvalidOperationException");
        body.ShouldNotContain("   at ");
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("title").GetString().ShouldBe("An unexpected error occurred");
        json.RootElement.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
    }
}
