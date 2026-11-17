using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrackManagement.Domain.Constants;
using TrackManagement.Infrastructure.Persistence;

namespace TrackManagement.Infrastructure.Identity;

/// <summary>
/// Creates the Admin role and the configured admin account if missing. An existing account is
/// never modified. Missing settings or a password that breaks the rules stop startup; messages
/// name the setting, never its value.
/// </summary>
public sealed partial class AdminSeeder(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IOptions<AdminOptions> options,
    ILogger<AdminSeeder> logger) : ISeeder
{
    public int Order => 10;

    public async Task SeedAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Email))
        {
            throw new InvalidOperationException("Admin:Email is required to provision the admin account (set ADMIN__EMAIL in .env).");
        }

        if (string.IsNullOrWhiteSpace(settings.Password))
        {
            throw new InvalidOperationException("Admin:Password is required to provision the admin account (set ADMIN__PASSWORD in .env).");
        }

        if (!await roleManager.RoleExistsAsync(Roles.Admin))
        {
            EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Admin) { Id = Guid.CreateVersion7() }), "create the Admin role");
        }

        var email = settings.Email.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            LogExists(logger);
            return;
        }

        var admin = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, LockoutEnabled = true };
        var created = await userManager.CreateAsync(admin, settings.Password);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                "Admin:Password does not meet the password rules: " + string.Join(" ", created.Errors.Select(e => e.Description)));
        }

        EnsureSucceeded(await userManager.AddToRoleAsync(admin, Roles.Admin), "add the admin to the Admin role");
        LogCreated(logger, admin.Id);
    }

    private static void EnsureSucceeded(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Could not {action}: " + string.Join(" ", result.Errors.Select(e => e.Code)));
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Admin account already exists; left unchanged")]
    private static partial void LogExists(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Admin account {UserId} created")]
    private static partial void LogCreated(ILogger logger, Guid userId);
}
