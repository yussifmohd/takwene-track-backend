using Ardalis.Result;
using TrackManagement.Application.Common.Messaging;
using TrackManagement.Application.Common.Persistence;
using TrackManagement.Application.Features.Tracks.Commands.ChangeTrackStatus;
using TrackManagement.Application.Features.Tracks.Specifications;
using TrackManagement.Domain.Tracks;

namespace TrackManagement.Application.Features.Tracks.Queries.GetTrack;

public sealed record GetTrackQuery(Guid Id) : IQuery<Result<TrackDetailDto>>;

public sealed class GetTrackQueryHandler(IReadRepository<Track> tracks) : IQueryHandler<GetTrackQuery, Result<TrackDetailDto>>
{
    public async Task<Result<TrackDetailDto>> Handle(GetTrackQuery request, CancellationToken cancellationToken)
    {
        var track = await tracks.FirstOrDefaultAsync(new TrackDetailSpec(request.Id), cancellationToken);
        return track is null ? Result.NotFound(ChangeTrackStatusCommandHandler.NotFoundMessage) : Result.Success(track);
    }
}
