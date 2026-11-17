using TrackManagement.Application.Common.Persistence;
using TrackManagement.Application.Features.Dsps.Specifications;
using TrackManagement.Domain.Dsps;

namespace TrackManagement.Application.Features.Dsps;

public static class DspNames
{
    /// <summary>Every platform's name by id (a few rows; one query).</summary>
    public static async Task<IReadOnlyDictionary<Guid, string>> LoadAsync(IReadRepository<Dsp> dsps, CancellationToken cancellationToken) =>
        (await dsps.ListAsync(new AllDspsSpec(), cancellationToken)).ToDictionary(d => d.Id, d => d.Name);
}
