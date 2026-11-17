using Ardalis.Result;
using FluentValidation;
using TrackManagement.Application.Common.Messaging;
using TrackManagement.Application.Common.Persistence;
using TrackManagement.Application.Common.Validation;
using TrackManagement.Application.Features.Tracks.Specifications;
using TrackManagement.Domain.Tracks;
using PagedTracks = TrackManagement.Application.Common.Models.PagedResult<TrackManagement.Application.Features.Tracks.TrackListItemDto>;

namespace TrackManagement.Application.Features.Tracks.Queries.ListTracks;

/// <param name="Genre">Exact genre name, or null/empty for all.</param>
/// <param name="Status">Status name ("submitted"), or null/empty for all.</param>
public sealed record ListTracksQuery(int Page, int PageSize, Guid? ArtistId = null, string? Genre = null, string? Status = null)
    : IQuery<Result<PagedTracks>>;

public sealed class ListTracksQueryValidator : AbstractValidator<ListTracksQuery>
{
    public ListTracksQueryValidator()
    {
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
        RuleFor(x => x.Genre).KnownGenre().When(x => !string.IsNullOrEmpty(x.Genre));
        RuleFor(x => x.Status).KnownStatus().When(x => !string.IsNullOrEmpty(x.Status));
    }
}

public sealed class ListTracksQueryHandler(IReadRepository<Track> tracks) : IQueryHandler<ListTracksQuery, Result<PagedTracks>>
{
    public async Task<Result<PagedTracks>> Handle(ListTracksQuery request, CancellationToken cancellationToken)
    {
        var filter = new TrackFilter(
            request.ArtistId,
            string.IsNullOrEmpty(request.Genre) ? null : request.Genre,
            TrackStatusNames.TryParse(request.Status, out var status) ? status : null);

        var total = await tracks.CountAsync(new TracksCountSpec(filter), cancellationToken);
        var items = await tracks.ListAsync(new TracksPageSpec(filter, request.Page, request.PageSize), cancellationToken);
        return Result.Success(new PagedTracks(items, request.Page, request.PageSize, total));
    }
}
