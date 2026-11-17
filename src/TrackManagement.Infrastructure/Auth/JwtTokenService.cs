using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TrackManagement.Application.Common.Security;

namespace TrackManagement.Infrastructure.Auth;

/// <summary>Issues HS256 access tokens: sub, email, role(s), jti; iss/aud; exp = now + Jwt:ExpirationMinutes.</summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : IJwtTokenService
{
    private static readonly JsonWebTokenHandler Handler = new();

    public AccessToken Create(AuthenticatedUser user)
    {
        var settings = options.Value;
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(settings.ExpirationMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                [JwtClaims.Subject] = user.Id.ToString(),
                [JwtClaims.Email] = user.Email,
                [JwtClaims.Role] = user.Roles.ToArray(),
                [JwtClaims.TokenId] = Guid.NewGuid().ToString("N"),
            },
            SigningCredentials = new SigningCredentials(JwtValidation.CreateKey(settings.SigningKey), SecurityAlgorithms.HmacSha256),
        };

        // Whole seconds: the exp claim has second precision, so report the same instant.
        var exp = DateTimeOffset.FromUnixTimeSeconds(expiresAt.ToUnixTimeSeconds());
        return new AccessToken(Handler.CreateToken(descriptor), exp);
    }
}

public static class JwtClaims
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Role = "role";
    public const string TokenId = "jti";
    public const string Expires = "exp";
}

/// <summary>The one place that defines how tokens are validated (user rules: iss, aud, lifetime, signature, HS256 only, skew ≤ 30 s).</summary>
public static class JwtValidation
{
    public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    public static SymmetricSecurityKey CreateKey(string signingKey) => new(Encoding.UTF8.GetBytes(signingKey));

    public static TokenValidationParameters CreateParameters(JwtOptions settings) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = settings.Issuer,
        ValidateAudience = true,
        ValidAudience = settings.Audience,
        ValidateLifetime = true,
        RequireExpirationTime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = CreateKey(settings.SigningKey),
        RequireSignedTokens = true,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ClockSkew = ClockSkew,
        NameClaimType = JwtClaims.Email,
        RoleClaimType = JwtClaims.Role,
    };
}
