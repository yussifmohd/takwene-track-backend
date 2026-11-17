using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace TrackManagement.Api.Health;

/// <summary>Writes <see cref="HealthReportDto"/> for the infrastructure endpoint <c>/health</c>.</summary>
public static class HealthResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        var services = context.RequestServices;
        var dto = HealthReportDto.From(report, services.GetRequiredService<TimeProvider>());
        var json = services.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;
        return context.Response.WriteAsJsonAsync(dto, json, context.RequestAborted);
    }
}
