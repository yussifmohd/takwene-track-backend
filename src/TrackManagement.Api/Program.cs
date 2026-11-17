using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using Serilog;
using TrackManagement.Api;
using TrackManagement.Api.Endpoints;
using TrackManagement.Api.Health;
using TrackManagement.Api.Security;
using TrackManagement.Application;
using TrackManagement.Infrastructure;
using TrackManagement.Infrastructure.Persistence;

// Startup logger until the host's Serilog configuration takes over. A plain logger (not
// CreateBootstrapLogger) because the reloadable logger can only be frozen once per process,
// which breaks multiple in-process hosts in the API tests.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // preserveStaticLogger: each host keeps its own logger. Without it the host logger replaces the
    // static Log.Logger and every in-process host (API tests) writes into the one started last.
    builder.Host.UseSerilog(
        (context, services, logger) => logger
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture),
        preserveStaticLogger: true);

    builder.Services
        .AddApplication()
        .AddInfrastructure(builder.Configuration)
        .AddApi(builder.Configuration);

    var app = builder.Build();

    await app.InitializeDatabaseAsync();

    // First, so everything after sees the real client IP (only from trusted proxies).
    app.UseForwardedHeaders();
    // Outside the exception handler, so the request log shows the status the client received
    // (inside it, a handled 400/413 was logged as "responded 500").
    // The host's logger (sinks + configuration), not the static startup logger the middleware defaults to.
    app.UseSerilogRequestLogging(options => options.Logger = app.Services.GetRequiredService<Serilog.ILogger>());
    app.UseExceptionHandler();
    app.UseRequestBodyLimit();
    app.UseStatusCodePages();
    app.UseCors(ApiServiceCollectionExtensions.CorsPolicyName);
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    if (app.Environment.IsDevelopment())
    {
        // API docs are only exposed in Development (constitution IV).
        app.MapOpenApi();
        app.MapScalarApiReference(options => options
            .WithTitle("Takwene Track Management API")
            .AddPreferredSecuritySchemes("Bearer"));
    }

    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = HealthResponseWriter.WriteAsync,
        ResultStatusCodes =
        {
            [Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy] = StatusCodes.Status200OK,
            [Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
            [Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
        },
    });

    app.MapApiEndpoints();

    await app.RunAsync();
    return 0;
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "API terminated during startup or shutdown");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>Entry point marker for WebApplicationFactory in tests.</summary>
public partial class Program;
