using Ardalis.Specification;
using TrackManagement.Domain.Dsps;

namespace TrackManagement.Application.Features.Dsps.Specifications;

public sealed class DspsSpec : Specification<Dsp, DspDto>
{
    public DspsSpec()
    {
        Query.AsNoTracking().OrderBy(d => d.Name);
        Query.Select(d => new DspDto(d.Id, d.Name));
    }
}
