using TrackManagement.Domain.Tracks;

namespace TrackManagement.Application.Features.Tracks;

/// <summary>A track in the catalog list. <see cref="Isrc"/> is hyphenated; <see cref="ReleaseDate"/> is a calendar date.</summary>
public sealed record TrackListItemDto(
    Guid Id,
    string Title,
    Guid ArtistId,
    string ArtistName,
    string Isrc,
    string Genre,
    TrackStatus Status,
    DateOnly ReleaseDate);

/// <summary>
/// A track's delivery to one platform. Always empty until Phase 4; a track marked distributed by a
/// status change alone has none (see the spec, "Status and distributions").
/// </summary>
public sealed record TrackDistributionDto(Guid DspId, string DspName, string Status, DateTime UpdatedAtUtc);

/// <summary>One track with everything the detail page needs, including the status changes allowed next.</summary>
public sealed record TrackDetailDto(
    Guid Id,
    string Title,
    Guid ArtistId,
    string ArtistName,
    string Isrc,
    string Genre,
    TrackStatus Status,
    DateOnly ReleaseDate,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyList<TrackStatus> AllowedNextStatuses,
    IReadOnlyList<TrackDistributionDto> Distributions)
{
    public static TrackDetailDto From(Track track, string artistName) => Create(
        track.Id, track.Title, track.ArtistId, artistName, track.Isrc, track.Genre, track.Status,
        track.ReleaseDate, track.CreatedAtUtc, track.UpdatedAtUtc);

    /// <summary>Shared by <see cref="From"/> and the detail projection, so both shape the DTO the same way.</summary>
    public static TrackDetailDto Create(
        Guid id, string title, Guid artistId, string artistName, string storedIsrc, string genre, TrackStatus status,
        DateOnly releaseDate, DateTime createdAtUtc, DateTime? updatedAtUtc) => new(
        id, title, artistId, artistName, Domain.Tracks.Isrc.Format(storedIsrc), genre, status, releaseDate,
        createdAtUtc, updatedAtUtc, TrackStatusRules.AllowedNext(status), []);
}
