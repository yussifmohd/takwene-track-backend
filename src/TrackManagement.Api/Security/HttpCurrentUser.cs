using System.Globalization;
using System.Security.Claims;
using TrackManagement.Application.Common.Security;
using TrackManagement.Infrastructure.Auth;

namespace TrackManagement.Api.Security;

/// <summary><see cref="ICurrentUser"/> read from the validated bearer token's claims.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId => Guid.TryParse(Principal?.FindFirstValue(JwtClaims.Subject), out var id) ? id : null;

    public string? Email => Principal?.FindFirstValue(JwtClaims.Email);

    public IReadOnlyList<string> Roles => Principal?.FindAll(JwtClaims.Role).Select(c => c.Value).ToList() ?? [];

    public DateTimeOffset? ExpiresAt =>
        long.TryParse(Principal?.FindFirstValue(JwtClaims.Expires), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;
}
