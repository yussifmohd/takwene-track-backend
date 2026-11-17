using Ardalis.Specification;
using TrackManagement.Domain.Tracks;

namespace TrackManagement.Application.Features.Tracks.Specifications;

/// <summary>Optional list filters, combined with AND. Null = not filtered.</summary>
public sealed record TrackFilter(Guid? ArtistId = null, string? Genre = null, TrackStatus? Status = null)
{
    public static readonly TrackFilter None = new();
}

internal static class TrackFilterExtensions
{
    /// <summary>One WHERE for both the count and the page, so totals always match the items.</summary>
    public static ISpecificationBuilder<Track> ApplyFilter(this ISpecificationBuilder<Track> query, TrackFilter filter)
    {
        if (filter.ArtistId is { } artistId)
        {
            query.Where(t => t.ArtistId == artistId);
        }

        if (filter.Genre is { } genre)
        {
            query.Where(t => t.Genre == genre);
        }

        if (filter.Status is { } status)
        {
            query.Where(t => t.Status == status);
        }

        return query;
    }
}

/// <summary>
/// One page of tracks: newest release first, then title ignoring case, then id (stable paging),
/// projected straight to <see cref="TrackListItemDto"/> with the artist's name.
/// </summary>
public sealed class TracksPageSpec : Specification<Track, TrackListItemDto>
{
    public TracksPageSpec(TrackFilter filter, int page, int pageSize)
    {
        Query.AsNoTracking().ApplyFilter(filter);
        Query.OrderByDescending(t => t.ReleaseDate)
#pragma warning disable CA1304, CA1311, CA1862 // Translated to SQL lower(); culture overloads are not translatable.
            .ThenBy(t => t.Title.ToLower())
#pragma warning restore CA1304, CA1311, CA1862
            .ThenBy(t => t.Id);
        Query.Skip((page - 1) * pageSize).Take(pageSize);
        Query.Select(t => new TrackListItemDto(t.Id, t.Title, t.ArtistId, t.Artist.Name, Isrc.Format(t.Isrc), t.Genre, t.Status, t.ReleaseDate));
    }
}

public sealed class TracksCountSpec : Specification<Track>
{
    public TracksCountSpec(TrackFilter filter) => Query.AsNoTracking().ApplyFilter(filter);
}

/// <summary>One track for reading, projected with the artist's name and its deliveries with platform names.</summary>
public sealed class TrackDetailSpec : Specification<Track, TrackDetailDto>, ISingleResultSpecification<Track, TrackDetailDto>
{
    public TrackDetailSpec(Guid id)
    {
        Query.AsNoTracking().Where(t => t.Id == id);
        Query.Select(t => TrackDetailDto.Create(
            t.Id, t.Title, t.ArtistId, t.Artist.Name, t.Isrc, t.Genre, t.Status, t.ReleaseDate, t.CreatedAtUtc, t.UpdatedAtUtc,
            t.Distributions
                .Select(d => new TrackDistributionRow(d.DspId, d.Dsp.Name, d.Status, d.SubmittedAtUtc, d.AnsweredAtUtc))
                .ToList()));
    }
}

/// <summary>One track to change (tracked), with its artist for the response and its deliveries (the aggregate).</summary>
public sealed class TrackForUpdateSpec : Specification<Track>, ISingleResultSpecification<Track>
{
    public TrackForUpdateSpec(Guid id) => Query.Where(t => t.Id == id).Include(t => t.Artist).Include(t => t.Distributions);
}

/// <summary>Tracks using the ISRC (already normalised to 12 characters).</summary>
public sealed class TrackByIsrcSpec : Specification<Track>
{
    public TrackByIsrcSpec(string normalizedIsrc) => Query.AsNoTracking().Where(t => t.Isrc == normalizedIsrc);
}
