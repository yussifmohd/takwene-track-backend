using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using TrackManagement.Infrastructure.Identity;

namespace TrackManagement.Infrastructure.Persistence;

/// <summary>
/// Used only by <c>dotnet ef</c> (migrations add/remove/script) so it does not need the API's
/// secrets. <c>database update</c> uses <c>ConnectionStrings__Default</c> when set.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=trackmgmt;Username=trackmgmt";

        // Identity reads its table layout version from IdentityOptions in the application services;
        // give design time the same version as runtime so migrations match the runtime model.
        var identityServices = new ServiceCollection()
            .Configure<IdentityOptions>(o => o.Stores.SchemaVersion = IdentitySetup.SchemaVersion)
            .BuildServiceProvider();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseApplicationServiceProvider(identityServices)
            .UseNpgsql(NpgsqlConnectionStringDefaults.Apply(connectionString))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options);
    }
}
