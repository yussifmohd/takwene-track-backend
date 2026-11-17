using Ardalis.Specification;
using TrackManagement.Domain.Artists;

namespace TrackManagement.Application.Features.Artists.Specifications;

/// <summary>The artist with this id (read-only).</summary>
public sealed class ArtistByIdSpec : Specification<Artist>, ISingleResultSpecification<Artist>
{
    public ArtistByIdSpec(Guid id) => Query.AsNoTracking().Where(a => a.Id == id);
}

/// <summary>One artist as a DTO; the email only for admins (as in the list).</summary>
public sealed class ArtistDetailSpec : Specification<Artist, ArtistDto>, ISingleResultSpecification<Artist, ArtistDto>
{
    public ArtistDetailSpec(Guid id, bool includeEmail)
    {
        Query.AsNoTracking().Where(a => a.Id == id);
        Query.Select(a => new ArtistDto(a.Id, a.Name, a.CountryCode, includeEmail ? a.Email : null, a.CreatedAtUtc));
    }
}
