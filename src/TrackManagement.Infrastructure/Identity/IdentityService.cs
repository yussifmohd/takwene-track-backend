using Ardalis.Result;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using TrackManagement.Application.Common.Security;
using TrackManagement.Application.Features.Auth;

namespace TrackManagement.Infrastructure.Identity;

/// <summary>
/// Credential check with Identity lockout. Every failure returns the same result; the reason is
/// only logged (with a masked email). Unknown emails still verify a password hash so they take
/// as long as a wrong password (no timing-based account enumeration).
/// </summary>
public sealed partial class IdentityService(
    UserManager<ApplicationUser> userManager,
    IPasswordHasher<ApplicationUser> passwordHasher,
    ILogger<IdentityService> logger) : IIdentityService
{
    // Computed once per process with the default (production) hasher settings.
    private static readonly Lazy<string> DummyPasswordHash = new(() =>
        new PasswordHasher<ApplicationUser>().HashPassword(new ApplicationUser(), Guid.NewGuid().ToString()));

    public async Task<Result<AuthenticatedUser>> AuthenticateAsync(string email, string password, CancellationToken cancellationToken)
    {
        var masked = MaskEmail(email);
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            passwordHasher.VerifyHashedPassword(new ApplicationUser(), DummyPasswordHash.Value, password);
            LogUnknownEmail(logger, masked);
            return Failed();
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            LogLockedOut(logger, user.Id, masked);
            return Failed();
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            if (await userManager.IsLockedOutAsync(user))
            {
                LogLockoutStarted(logger, user.Id, masked);
            }
            else
            {
                LogWrongPassword(logger, user.Id, masked);
            }

            return Failed();
        }

        await userManager.ResetAccessFailedCountAsync(user);
        var roles = await userManager.GetRolesAsync(user);
        LogSucceeded(logger, user.Id);
        return Result.Success(new AuthenticatedUser(user.Id, user.Email ?? email, roles.ToList()));
    }

    /// <summary>"admin@takwene.local" → "a***@takwene.local"; anything else → "***".</summary>
    public static string MaskEmail(string email)
    {
        var at = email.IndexOf('@', StringComparison.Ordinal);
        return at > 0 ? $"{email[0]}***{email[at..]}" : "***";
    }

    private static Result<AuthenticatedUser> Failed() => Result<AuthenticatedUser>.Unauthorized(AuthErrors.InvalidCredentials);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Sign-in succeeded for user {UserId}")]
    private static partial void LogSucceeded(ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning, Message = "Sign-in failed: unknown email {MaskedEmail}")]
    private static partial void LogUnknownEmail(ILogger logger, string maskedEmail);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Warning, Message = "Sign-in failed: wrong password for user {UserId} ({MaskedEmail})")]
    private static partial void LogWrongPassword(ILogger logger, Guid userId, string maskedEmail);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Warning, Message = "Sign-in refused: user {UserId} ({MaskedEmail}) is locked out")]
    private static partial void LogLockedOut(ILogger logger, Guid userId, string maskedEmail);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Warning, Message = "Sign-in failed: user {UserId} ({MaskedEmail}) is now locked out after repeated failures")]
    private static partial void LogLockoutStarted(ILogger logger, Guid userId, string maskedEmail);
}
