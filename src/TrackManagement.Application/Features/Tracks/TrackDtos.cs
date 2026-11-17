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
/// A track's delivery to one platform. <see cref="AnsweredAtUtc"/> is null while pending;
/// <see cref="AllowedAnswers"/> lists the simulated platform answers allowed now (only while pending).
/// </summary>
public sealed record TrackDistributionDto(
    Guid DspId,
    string DspName,
    DistributionStatus Status,
    DateTime SubmittedAtUtc,
    DateTime? AnsweredAtUtc,
    IReadOnlyList<DistributionStatus> AllowedAnswers);

/// <summary>A delivery as read from the database, before the allowed answers are added.</summary>
public sealed record TrackDistributionRow(Guid DspId, string DspName, DistributionStatus Status, DateTime SubmittedAtUtc, DateTime? AnsweredAtUtc);

/// <summary>
/// One track with everything the detail page needs: the status changes allowed next, whether it can be
/// distributed, and its deliveries ordered by platform name.
/// </summary>
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
    bool CanDistribute,
    IReadOnlyList<TrackDistributionDto> Distributions)
{
    /// <param name="dspNames">Platform names by id (all platforms; a few rows).</param>
    public static TrackDetailDto From(Track track, string artistName, IReadOnlyDictionary<Guid, string> dspNames) => Create(
        track.Id, track.Title, track.ArtistId, artistName, track.Isrc, track.Genre, track.Status,
        track.ReleaseDate, track.CreatedAtUtc, track.UpdatedAtUtc,
        track.Distributions
            .Select(d => new TrackDistributionRow(d.DspId, dspNames[d.DspId], d.Status, d.SubmittedAtUtc, d.AnsweredAtUtc))
            .ToList());

    /// <summary>Shared by <see cref="From"/> and the detail projection, so both shape the DTO the same way.</summary>
    public static TrackDetailDto Create(
        Guid id, string title, Guid artistId, string artistName, string storedIsrc, string genre, TrackStatus status,
        DateOnly releaseDate, DateTime createdAtUtc, DateTime? updatedAtUtc, IReadOnlyCollection<TrackDistributionRow> distributions) => new(
        id, title, artistId, artistName, Domain.Tracks.Isrc.Format(storedIsrc), genre, status, releaseDate,
        createdAtUtc, updatedAtUtc,
        TrackStatusRules.AllowedNext(status, distributions.Count > 0),
        status != TrackStatus.Draft,
        distributions
            .OrderBy(d => d.DspName, StringComparer.Ordinal)
            .ThenBy(d => d.DspId)
            .Select(d => new TrackDistributionDto(
                d.DspId, d.DspName, d.Status, d.SubmittedAtUtc, d.AnsweredAtUtc, DistributionStatusRules.AllowedAnswers(d.Status)))
            .ToList());
}
