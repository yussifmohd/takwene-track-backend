using System.ComponentModel.DataAnnotations;

namespace TrackManagement.Infrastructure.Auth;

/// <summary>
/// JWT settings, validated at startup so the API refuses to run with a missing or weak key.
/// The same settings issue tokens (JwtTokenService) and validate them (JwtValidation).
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required(AllowEmptyStrings = false)]
    public string Issuer { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Audience { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "Jwt:SigningKey is required (set Jwt__SigningKey, at least 32 characters).")]
    [MinLength(32, ErrorMessage = "Jwt:SigningKey must be at least 32 characters (256 bits) for HS256.")]
    public string SigningKey { get; init; } = string.Empty;

    [Range(5, 1440)]
    public int ExpirationMinutes { get; init; } = 60;
}
