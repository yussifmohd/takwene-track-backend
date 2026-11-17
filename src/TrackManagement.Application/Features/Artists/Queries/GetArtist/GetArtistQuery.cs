using Ardalis.Result;
using TrackManagement.Application.Common.Messaging;
using TrackManagement.Application.Common.Persistence;
using TrackManagement.Application.Features.Artists.Specifications;
using TrackManagement.Domain.Artists;

namespace TrackManagement.Application.Features.Artists.Queries.GetArtist;

/// <param name="IncludeEmail">True only for a signed-in admin (decided at the HTTP edge).</param>
public sealed record GetArtistQuery(Guid Id, bool IncludeEmail) : IQuery<Result<ArtistDto>>;

public sealed class GetArtistQueryHandler(IReadRepository<Artist> artists) : IQueryHandler<GetArtistQuery, Result<ArtistDto>>
{
    public async Task<Result<ArtistDto>> Handle(GetArtistQuery request, CancellationToken cancellationToken)
    {
        var artist = await artists.FirstOrDefaultAsync(new ArtistDetailSpec(request.Id, request.IncludeEmail), cancellationToken);
        return artist is null ? Result.NotFound("No artist has this id.") : Result.Success(artist);
    }
}
