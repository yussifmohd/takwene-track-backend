using Ardalis.Specification;
using TrackManagement.Domain.Artists;

namespace TrackManagement.Application.Features.Artists.Specifications;

/// <summary>
/// One page of artists ordered by name ignoring case, then by creation time and id (stable paging),
/// projected straight to <see cref="ArtistDto"/>. The email is only selected for admins.
/// </summary>
public sealed class ArtistsPageSpec : Specification<Artist, ArtistDto>
{
    public ArtistsPageSpec(int page, int pageSize, bool includeEmail)
    {
        Query.AsNoTracking()
#pragma warning disable CA1304, CA1311, CA1862 // Translated to SQL lower(); culture overloads are not translatable.
            .OrderBy(a => a.Name.ToLower())
#pragma warning restore CA1304, CA1311, CA1862
            .ThenBy(a => a.CreatedAtUtc)
            .ThenBy(a => a.Id);
        Query.Skip((page - 1) * pageSize).Take(pageSize);
        Query.Select(a => new ArtistDto(a.Id, a.Name, a.CountryCode, includeEmail ? a.Email : null, a.CreatedAtUtc));
    }
}

/// <summary>All artists (no filters yet); used for the total of a page.</summary>
public sealed class ArtistsCountSpec : Specification<Artist>
{
    public ArtistsCountSpec() => Query.AsNoTracking();
}
