using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using TrackManagement.Infrastructure.Persistence;

namespace TrackManagement.UnitTests.Api;

/// <summary>
/// In-process API host for HTTP-level tests: EF InMemory instead of Postgres, test configuration,
/// and optional extra services/routes. Environment defaults to "Testing" (no API docs).
/// </summary>
public sealed class TestApiFactory(string environment = "Testing", Action<IServiceCollection>? configureServices = null)
    : WebApplicationFactory<Program>
{
    public const string ThrowingPath = "/test/throw";
    public const string SecretExceptionMessage = "super-secret-internal-detail";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("ConnectionStrings:Default", "Host=unused-in-tests");
        builder.UseSetting("Jwt:SigningKey", new string('k', 48));
        builder.UseSetting("Database:MigrateOnStartup", "false");

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

            // Test-only route that throws, appended after the app pipeline so the global handler wraps it.
            services.AddTransient<IStartupFilter, ThrowingRouteStartupFilter>();

            configureServices?.Invoke(services);
        });
    }

    private sealed class ThrowingRouteStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.Map(ThrowingPath, branch => branch.Run(_ => throw new InvalidOperationException(SecretExceptionMessage)));
        };
    }
}
