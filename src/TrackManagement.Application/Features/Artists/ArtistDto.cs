using System.Text.Json.Serialization;

namespace TrackManagement.Application.Features.Artists;

/// <summary>An artist as returned by the API. <see cref="Email"/> is only filled for a signed-in admin and omitted from JSON otherwise.</summary>
public sealed record ArtistDto(
    Guid Id,
    string Name,
    string CountryCode,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Email,
    DateTime CreatedAtUtc);
