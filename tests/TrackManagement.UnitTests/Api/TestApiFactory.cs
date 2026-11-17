using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using TrackManagement.Api.Security;
using TrackManagement.Infrastructure.Persistence;
using TrackManagement.UnitTests.TestSupport;

namespace TrackManagement.UnitTests.Api;

/// <summary>
/// In-process API host for HTTP-level tests: EF InMemory instead of Postgres, test configuration,
/// the admin seeded on startup, and TEST-ONLY endpoints (defined here, never in production code).
/// Environment defaults to "Testing" (no API docs).
/// </summary>
public sealed class TestApiFactory(
    string environment = "Testing",
    Action<IServiceCollection>? configureServices = null,
    IReadOnlyDictionary<string, string?>? settings = null)
    : WebApplicationFactory<Program>
{
    public const string ThrowingPath = "/test/throw";
    public const string AdminOnlyPath = "/test/admin-only";
    public const string PublicPath = "/test/public";
    public const string SecretExceptionMessage = "super-secret-internal-detail";

    /// <summary>Sets the connection's remote IP (TestServer has none) to exercise forwarded headers and rate limits.</summary>
    public const string ClientIpHeader = "X-Test-Client-IP";

    public const string AdminEmail = "admin@takwene.test";
    public const string AdminPassword = "Adm1n!Passw0rd#2026";

    public CollectingLogSink Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("ConnectionStrings:Default", "Host=unused-in-tests");
        builder.UseSetting("Jwt:SigningKey", TestTokens.SigningKey);
        builder.UseSetting("Jwt:Issuer", TestTokens.Issuer);
        builder.UseSetting("Jwt:Audience", TestTokens.Audience);
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Admin:Email", AdminEmail);
        builder.UseSetting("Admin:Password", AdminPassword);
        builder.UseSetting("RateLimiting:Login:PermitLimit", "1000");
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            // Swap Npgsql for EF InMemory: remove every options registration for AppDbContext first.
            var toRemove = services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    (d.ServiceType.IsGenericType &&
                     d.ServiceType.GetGenericTypeDefinition() == typeof(IDbContextOptionsConfiguration<>) &&
                     d.ServiceType.GenericTypeArguments[0] == typeof(AppDbContext)))
                .ToList();
            foreach (var descriptor in toRemove)
            {
                services.Remove(descriptor);
            }

            var dbName = $"api-tests-{Guid.NewGuid()}";
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));

            services.AddSingleton<ILogEventSink>(Logs);
            services.AddTransient<IStartupFilter, TestPipelineStartupFilter>();

            configureServices?.Invoke(services);
        });
    }

    private sealed class TestPipelineStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                if (IPAddress.TryParse(context.Request.Headers[ClientIpHeader], out var ip))
                {
                    context.Connection.RemoteIpAddress = ip;
                }

                return nextMiddleware(context);
            });

            next(app);

            // Test-only routes, reached when the app pipeline has no matching endpoint. Excluded from the
            // OpenAPI description so they never appear in the exported contract.
            app.Map(ThrowingPath, branch => branch.Run(_ => throw new InvalidOperationException(SecretExceptionMessage)));
            app.Map("/test", branch =>
            {
                branch.UseRouting();
                branch.UseAuthorization();
                branch.UseEndpoints(endpoints =>
                {
                    endpoints.MapPost("/admin-only", () => Results.Ok(new { ok = true })).RequireAdmin().ExcludeFromDescription();
                    endpoints.MapGet("/public", () => Results.Ok(new { ok = true })).ExcludeFromDescription();
                });
            });
        };
    }
}
