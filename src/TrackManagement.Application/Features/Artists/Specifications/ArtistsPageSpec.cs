using Ardalis.Specification;
using TrackManagement.Domain.Artists;

namespace TrackManagement.Application.Features.Artists.Specifications;

/// <summary>
/// One page of artists ordered by name ignoring case, then by creation time and id (stable paging),
/// projected straight to <see cref="ArtistDto"/>. The email is only selected for admins.
/// </summary>
public sealed class ArtistsPageSpec : Specification<Artist, ArtistDto>
{
    public ArtistsPageSpec(int page, int pageSize, bool includeEmail, string? search = null)
    {
        Query.AsNoTracking().MatchingName(search);
        Query
#pragma warning disable CA1304, CA1311, CA1862 // Translated to SQL lower(); culture overloads are not translatable.
            .OrderBy(a => a.Name.ToLower())
#pragma warning restore CA1304, CA1311, CA1862
            .ThenBy(a => a.CreatedAtUtc)
            .ThenBy(a => a.Id);
        Query.Skip((page - 1) * pageSize).Take(pageSize);
        Query.Select(a => new ArtistDto(a.Id, a.Name, a.CountryCode, includeEmail ? a.Email : null, a.CreatedAtUtc));
    }
}

/// <summary>Artists matching the same search as the page; used for the total.</summary>
public sealed class ArtistsCountSpec : Specification<Artist>
{
    public ArtistsCountSpec(string? search = null) => Query.AsNoTracking().MatchingName(search);
}

internal static class ArtistSearchExtensions
{
    /// <summary>
    /// Name contains <paramref name="search"/>, ignoring letter case. EF translates <c>Contains</c> to
    /// <c>strpos</c>, so <c>%</c> and <c>_</c> are matched literally (no LIKE wildcards).
    /// </summary>
    public static ISpecificationBuilder<Artist> MatchingName(this ISpecificationBuilder<Artist> query, string? search)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
#pragma warning disable CA1304, CA1311, CA1862 // Translated to SQL lower(); culture overloads are not translatable.
            var term = search.Trim().ToLower();
            query.Where(a => a.Name.ToLower().Contains(term));
#pragma warning restore CA1304, CA1311, CA1862
        }

        return query;
    }
}
