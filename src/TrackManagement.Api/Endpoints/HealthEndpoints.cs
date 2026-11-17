using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TrackManagement.Api.Health;

namespace TrackManagement.Api.Endpoints;

public static class HealthEndpoints
{
    /// <summary>
    /// <c>GET /api/health</c>: the contract endpoint used by the web app (same origin via the /api proxy).
    /// Infrastructure probes use <c>/health</c>, which shares the same body. Health is a framework
    /// concern, so this endpoint calls HealthCheckService directly (plan: Complexity Tracking).
    /// </summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/health").WithTags("Health");

        group.MapGet("/", async Task<Results<Ok<HealthReportDto>, JsonHttpResult<HealthReportDto>>> (
                HealthCheckService healthChecks, TimeProvider timeProvider, CancellationToken cancellationToken) =>
            {
                var report = await healthChecks.CheckHealthAsync(cancellationToken);
                var dto = HealthReportDto.From(report, timeProvider);
                return report.Status == HealthStatus.Healthy
                    ? TypedResults.Ok(dto)
                    : TypedResults.Json(dto, statusCode: StatusCodes.Status503ServiceUnavailable);
            })
            .WithName("GetHealth")
            .WithSummary("Get API and database health")
            .Produces<HealthReportDto>(StatusCodes.Status503ServiceUnavailable)
            .AllowAnonymous();

        return app;
    }
}
