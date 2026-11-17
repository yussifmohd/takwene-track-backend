using MediatR;
using Microsoft.AspNetCore.Mvc;
using TrackManagement.Api.Extensions;
using TrackManagement.Api.Security;
using TrackManagement.Application.Common.Security;
using TrackManagement.Application.Features.Artists;
using TrackManagement.Application.Features.Artists.Commands.CreateArtist;
using TrackManagement.Application.Features.Artists.Queries.ListArtists;
using TrackManagement.Domain.Constants;
using PagedArtists = TrackManagement.Application.Common.Models.PagedResult<TrackManagement.Application.Features.Artists.ArtistDto>;

namespace TrackManagement.Api.Endpoints;

/// <summary>Create-artist body. All fields required; bound here, never onto the entity.</summary>
public sealed record CreateArtistRequest(string? Name, string? CountryCode, string? Email);

/// <summary>Paging query string. Missing values use the defaults (page 1, 20 per page).</summary>
public sealed record ListArtistsRequest([FromQuery(Name = "page")] int? Page, [FromQuery(Name = "pageSize")] int? PageSize);

public static class ArtistEndpoints
{
    public static IEndpointRouteBuilder MapArtistEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/artists").WithTags("Artists");

        group.MapGet("/", async ([AsParameters] ListArtistsRequest request, ICurrentUser currentUser, ISender sender, CancellationToken cancellationToken) =>
            {
                // Emails are personal data: only a signed-in admin gets them (reads are otherwise public).
                var includeEmail = currentUser.IsAuthenticated && currentUser.Roles.Contains(Roles.Admin);
                var query = new ListArtistsQuery(request.Page ?? 1, request.PageSize ?? PagedArtists.DefaultPageSize, includeEmail);
                return (await sender.Send(query, cancellationToken)).ToHttpResult();
            })
            .WithName("ListArtists")
            .WithSummary("List artists by name, page by page (email only for admins)")
            .AllowAnonymous()
            .Produces<PagedArtists>()
            .ProducesValidationProblem();

        group.MapPost("/", async (CreateArtistRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var command = new CreateArtistCommand(request.Name ?? string.Empty, request.CountryCode ?? string.Empty, request.Email ?? string.Empty);
                return (await sender.Send(command, cancellationToken)).ToCreatedAtHttpResult(artist => $"/api/artists/{artist.Id}");
            })
            .WithName("CreateArtist")
            .WithSummary("Add an artist (admin only)")
            .RequireAdmin()
            .Produces<ArtistDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesValidationProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge);

        return app;
    }
}
