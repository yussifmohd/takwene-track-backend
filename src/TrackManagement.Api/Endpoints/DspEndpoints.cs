using MediatR;
using TrackManagement.Api.Extensions;
using TrackManagement.Application.Features.Dsps;
using TrackManagement.Application.Features.Dsps.Queries.ListDsps;

namespace TrackManagement.Api.Endpoints;

public static class DspEndpoints
{
    public static IEndpointRouteBuilder MapDspEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/dsps").WithTags("DSPs")
            .MapGet("/", async (ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new ListDspsQuery(), cancellationToken)).ToHttpResult())
            .WithName("ListDsps")
            .WithSummary("List the distribution platforms (fixed set)")
            .AllowAnonymous()
            .Produces<IReadOnlyList<DspDto>>();
        return app;
    }
}
