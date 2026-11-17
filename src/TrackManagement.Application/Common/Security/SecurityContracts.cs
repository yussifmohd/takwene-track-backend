using Ardalis.Result;

namespace TrackManagement.Application.Common.Security;

/// <summary>A user whose credentials were verified.</summary>
public sealed record AuthenticatedUser(Guid Id, string Email, IReadOnlyList<string> Roles);

/// <summary>A signed access token and its absolute expiry.</summary>
public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>Verifies credentials (with lockout). Implemented with ASP.NET Core Identity.</summary>
public interface IIdentityService
{
    /// <summary>
    /// Every failure (unknown email, wrong password, locked out) returns the same
    /// <see cref="ResultStatus.Unauthorized"/> result so callers cannot tell them apart.
    /// </summary>
    Task<Result<AuthenticatedUser>> AuthenticateAsync(string email, string password, CancellationToken cancellationToken);
}

/// <summary>Issues signed access tokens.</summary>
public interface IJwtTokenService
{
    AccessToken Create(AuthenticatedUser user);
}

/// <summary>The caller of the current request, from the validated access token.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    string? Email { get; }

    IReadOnlyList<string> Roles { get; }

    DateTimeOffset? ExpiresAt { get; }
}
