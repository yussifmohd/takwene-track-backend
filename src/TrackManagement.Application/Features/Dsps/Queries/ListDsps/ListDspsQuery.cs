using Ardalis.Result;
using TrackManagement.Application.Common.Messaging;
using TrackManagement.Application.Common.Persistence;
using TrackManagement.Application.Features.Dsps.Specifications;
using TrackManagement.Domain.Dsps;

namespace TrackManagement.Application.Features.Dsps.Queries.ListDsps;

public sealed record ListDspsQuery : IQuery<Result<IReadOnlyList<DspDto>>>;

public sealed class ListDspsQueryHandler(IReadRepository<Dsp> dsps) : IQueryHandler<ListDspsQuery, Result<IReadOnlyList<DspDto>>>
{
    public async Task<Result<IReadOnlyList<DspDto>>> Handle(ListDspsQuery request, CancellationToken cancellationToken) =>
        Result.Success<IReadOnlyList<DspDto>>(await dsps.ListAsync(new DspsSpec(), cancellationToken));
}
