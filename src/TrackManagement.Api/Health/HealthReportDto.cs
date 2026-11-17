using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TrackManagement.Api.Health;

public enum HealthState
{
    Healthy,
    Degraded,
    Unhealthy,
}

public sealed record HealthCheckEntryDto(string Name, HealthState Status, double DurationMs);

/// <summary>Health report returned by <c>/health</c> and <c>/api/health</c>. Never contains exception text.</summary>
public sealed record HealthReportDto(HealthState Status, DateTimeOffset CheckedAt, IReadOnlyList<HealthCheckEntryDto> Checks)
{
    public static HealthReportDto From(HealthReport report, TimeProvider timeProvider) => new(
        Map(report.Status),
        timeProvider.GetUtcNow(),
        report.Entries
            .Select(e => new HealthCheckEntryDto(e.Key, Map(e.Value.Status), Math.Round(e.Value.Duration.TotalMilliseconds, 1)))
            .ToList());

    public static int StatusCodeFor(HealthStatus status) =>
        status == HealthStatus.Healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable;

    private static HealthState Map(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => HealthState.Healthy,
        HealthStatus.Degraded => HealthState.Degraded,
        _ => HealthState.Unhealthy,
    };
}
