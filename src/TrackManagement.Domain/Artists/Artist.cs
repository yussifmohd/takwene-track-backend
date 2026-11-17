using TrackManagement.Domain.Common;
using TrackManagement.Domain.Countries;

namespace TrackManagement.Domain.Artists;

/// <summary>A performer credited on tracks. Values are validated before they get here; the guards are a last line of defence.</summary>
public sealed class Artist : BaseEntity<Guid>, IAuditableEntity
{
    public const int NameMaxLength = 200;
    public const int EmailMaxLength = 254;

    private Artist()
    {
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2, upper case.</summary>
    public string CountryCode { get; private set; } = string.Empty;

    /// <summary>Contact email as entered (trimmed). Shown to admins only.</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>Upper-cased email used for the case-insensitive unique index. Never exposed.</summary>
    public string NormalizedEmail { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public static Artist Create(string name, string countryCode, string email, DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        if (!CountryCodes.IsAssigned(countryCode))
        {
            throw new ArgumentException($"'{countryCode}' is not an assigned ISO 3166-1 alpha-2 code.", nameof(countryCode));
        }

        if (createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Creation time must be UTC.", nameof(createdAtUtc));
        }

        var trimmedEmail = email.Trim();
        return new Artist
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            CountryCode = CountryCodes.Normalize(countryCode),
            Email = trimmedEmail,
            NormalizedEmail = NormalizeEmail(trimmedEmail),
            CreatedAtUtc = createdAtUtc,
        };
    }

    /// <summary>"  Booking@Example.com " → "BOOKING@EXAMPLE.COM" (same rule as ASP.NET Core Identity).</summary>
    public static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();
}
