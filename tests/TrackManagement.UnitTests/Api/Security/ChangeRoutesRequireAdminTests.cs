using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using TrackManagement.Api.Security;

namespace TrackManagement.UnitTests.Api.Security;

/// <summary>
/// Every route that changes data is checked from the app's real route table, so a route added later
/// without <c>.RequireAdminWrite()</c> fails here instead of shipping open.
/// </summary>
public sealed class ChangeRoutesRequireAdminTests
{
    private static readonly string[] ChangeMethods = ["POST", "PUT", "PATCH", "DELETE"];

    /// <summary>Change routes that are open on purpose: signing in.</summary>
    private static readonly string[] OpenOnPurpose = ["POST /api/auth/login"];

    private static List<(string Method, RouteEndpoint Endpoint)> ChangeRoutes(TestApiFactory factory) =>
        factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/", StringComparison.Ordinal) == true)
            .SelectMany(e => (e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [])
                .Where(m => ChangeMethods.Contains(m))
                .Select(m => (m, e)))
            .Where(r => !OpenOnPurpose.Contains($"{r.m} {r.e.RoutePattern.RawText}"))
            .ToList();

    [Fact]
    public async Task EveryChangeRoute_RequiresTheAdminPolicy_AndTheWriteLimit()
    {
        // Arrange
        await using var factory = new TestApiFactory();
        _ = factory.CreateClient();

        // Act
        var routes = ChangeRoutes(factory);

        // Assert
        routes.Count.ShouldBeGreaterThanOrEqualTo(5, "artists create, tracks create/status/distribute/answer");
        foreach (var (method, endpoint) in routes)
        {
            var name = $"{method} {endpoint.RoutePattern.RawText}";
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().ShouldContain(a => a.Policy == AuthPolicies.Admin, name);
            endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName.ShouldBe(ApiRateLimiting.WritesPolicy, name);
            endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>().ShouldNotBeNull(name);
        }
    }

    [Fact]
    public async Task EveryChangeRoute_AnswersAnonymousCallsWith401()
    {
        // Arrange
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        foreach (var (method, endpoint) in ChangeRoutes(factory))
        {
            var path = string.Join('/', endpoint.RoutePattern.PathSegments.Select(segment =>
                segment.IsSimple && segment.Parts[0] is Microsoft.AspNetCore.Routing.Patterns.RoutePatternLiteralPart literal
                    ? literal.Content
                    : Guid.NewGuid().ToString()));
            using var request = new HttpRequestMessage(new HttpMethod(method), "/" + path)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
            };

            // Act
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, $"{method} /{path}");
        }
    }
}
