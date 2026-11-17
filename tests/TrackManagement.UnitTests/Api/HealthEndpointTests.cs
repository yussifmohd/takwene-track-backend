using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TrackManagement.UnitTests.Api;

public sealed class HealthEndpointTests
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/api/health")]
    public async Task Get_WhenDatabaseReachable_Returns200WithContractBody(string path)
    {
        // Arrange
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = json.RootElement;
        root.GetProperty("status").GetString().ShouldBe("healthy");
        root.GetProperty("checkedAt").GetDateTimeOffset().ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1));
        var check = root.GetProperty("checks").EnumerateArray().ShouldHaveSingleItem();
        check.GetProperty("name").GetString().ShouldBe("database");
        check.GetProperty("status").GetString().ShouldBe("healthy");
        check.GetProperty("durationMs").GetDouble().ShouldBeGreaterThanOrEqualTo(0);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/api/health")]
    public async Task Get_WhenACheckFails_Returns503UnhealthyWithoutExceptionText(string path)
    {
        // Arrange
        await using var factory = new TestApiFactory(configureServices: services => services
            .AddHealthChecks()
            .AddCheck("failing", () => HealthCheckResult.Unhealthy("secret-description", new InvalidOperationException("secret-exception"))));
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("status").GetString().ShouldBe("unhealthy");
        json.RootElement.GetProperty("checks").EnumerateArray()
            .ShouldContain(c => c.GetProperty("name").GetString() == "failing" && c.GetProperty("status").GetString() == "unhealthy");
        body.ShouldNotContain("secret");
    }
}
