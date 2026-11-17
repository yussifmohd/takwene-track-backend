using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrackManagement.Application.Common.Security;
using TrackManagement.Infrastructure.Persistence;

namespace TrackManagement.Infrastructure.Identity;

public static class IdentitySetup
{
    public const int PasswordMinLength = 12;
    public const int MaxFailedAccessAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    /// <summary>Identity table layout; also pinned in AppDbContext so migrations match runtime.</summary>
    public static readonly Version SchemaVersion = IdentitySchemaVersions.Version2;

    /// <summary>Identity core (no cookies): users, roles, EF stores, password and lockout rules.</summary>
    public static IServiceCollection AddAppIdentity(this IServiceCollection services)
    {
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = PasswordMinLength;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = MaxFailedAccessAttempts;
                options.Lockout.DefaultLockoutTimeSpan = LockoutDuration;

                options.User.RequireUniqueEmail = true;
                options.Stores.SchemaVersion = SchemaVersion;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IIdentityService, IdentityService>();
        return services;
    }
}
