using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrackManagement.Infrastructure.Identity;
using TrackManagement.Infrastructure.Persistence;

namespace TrackManagement.UnitTests.TestSupport;

/// <summary>Real Identity (production rules) on EF InMemory, for service-level tests.</summary>
public static class TestIdentity
{
    public static ServiceProvider Build(ListLoggerProvider? logs = null, Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(b =>
        {
            b.SetMinimumLevel(LogLevel.Trace);
            if (logs is not null)
            {
                b.AddProvider(logs);
            }
        });
        var dbName = $"identity-tests-{Guid.NewGuid()}";
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddAppIdentity();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    public static async Task<ApplicationUser> CreateUserAsync(IServiceProvider services, string email, string password, params string[] roles)
    {
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var rolesManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        (await users.CreateAsync(user, password)).Succeeded.ShouldBeTrue();
        foreach (var role in roles)
        {
            if (!await rolesManager.RoleExistsAsync(role))
            {
                await rolesManager.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.NewGuid() });
            }

            await users.AddToRoleAsync(user, role);
        }

        return user;
    }
}

/// <summary>Counts hash verifications while delegating to the real hasher.</summary>
public sealed class CountingPasswordHasher : IPasswordHasher<ApplicationUser>
{
    private readonly PasswordHasher<ApplicationUser> _inner = new();

    public int VerifyCalls { get; private set; }

    public void ResetCalls() => VerifyCalls = 0;

    public string HashPassword(ApplicationUser user, string password) => _inner.HashPassword(user, password);

    public PasswordVerificationResult VerifyHashedPassword(ApplicationUser user, string hashedPassword, string providedPassword)
    {
        VerifyCalls++;
        return _inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
    }
}

/// <summary>A TimeProvider fixed at a given instant.</summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
