using Ardalis.Specification;
using TrackManagement.Domain.Artists;

namespace TrackManagement.Application.Features.Artists.Specifications;

/// <summary>Artists whose email equals <paramref name="email"/> ignoring letter case and outer spaces.</summary>
public sealed class ArtistByNormalizedEmailSpec : Specification<Artist>
{
    public ArtistByNormalizedEmailSpec(string email)
    {
        var normalized = Artist.NormalizeEmail(email);
        Query.AsNoTracking().Where(a => a.NormalizedEmail == normalized);
    }
}
