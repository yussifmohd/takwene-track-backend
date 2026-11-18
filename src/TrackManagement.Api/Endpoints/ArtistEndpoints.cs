using MediatR;
using Microsoft.AspNetCore.Mvc;
using TrackManagement.Api.Extensions;
using TrackManagement.Api.Security;
using TrackManagement.Application.Common.Security;
using TrackManagement.Application.Features.Artists;
using TrackManagement.Application.Features.Artists.Commands.CreateArtist;
using TrackManagement.Application.Features.Artists.Queries.GetArtist;
using TrackManagement.Application.Features.Artists.Queries.ListArtists;
using TrackManagement.Domain.Constants;
using PagedArtists = TrackManagement.Application.Common.Models.PagedResult<TrackManagement.Application.Features.Artists.ArtistDto>;

namespace TrackManagement.Api.Endpoints;

/// <summary>Create-artist body. All fields required; bound here, never onto the entity.</summary>
public sealed record CreateArtistRequest(string? Name, string? CountryCode, string? Email);

/// <summary>Paging and search query string. Missing values use the defaults (page 1, 20 per page, everyone).</summary>
public sealed record ListArtistsRequest(
    [FromQuery(Name = "page")] int? Page,
    [FromQuery(Name = "pageSize")] int? PageSize,
    [FromQuery(Name = "search")] string? Search);

public static class ArtistEndpoints
{
    public static IEndpointRouteBuilder MapArtistEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/artists").WithTags("Artists");

        group.MapGet("/", async ([AsParameters] ListArtistsRequest request, ICurrentUser currentUser, ISender sender, CancellationToken cancellationToken) =>
            {
                var query = new ListArtistsQuery(request.Page ?? 1, request.PageSize ?? PagedArtists.DefaultPageSize, IsAdmin(currentUser), request.Search);
                return (await sender.Send(query, cancellationToken)).ToHttpResult();
            })
            .WithName("ListArtists")
            .WithSummary("List artists by name, page by page, optionally only names containing a search text (email only for admins)")
            .AllowAnonymous()
            .Produces<PagedArtists>()
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", async (Guid id, ICurrentUser currentUser, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new GetArtistQuery(id, IsAdmin(currentUser)), cancellationToken)).ToHttpResult())
            .WithName("GetArtist")
            .WithSummary("Get one artist (email only for admins)")
            .AllowAnonymous()
            .Produces<ArtistDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (CreateArtistRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var command = new CreateArtistCommand(request.Name ?? string.Empty, request.CountryCode ?? string.Empty, request.Email ?? string.Empty);
                return (await sender.Send(command, cancellationToken)).ToCreatedAtHttpResult(artist => $"/api/artists/{artist.Id}");
            })
            .WithName("CreateArtist")
            .WithSummary("Add an artist (admin only)")
            .RequireAdminWrite()
            .Produces<ArtistDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesValidationProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge);

        return app;
    }

    /// <summary>Emails are personal data: only a signed-in admin gets them (reads are otherwise public).</summary>
    private static bool IsAdmin(ICurrentUser currentUser) => currentUser.IsAuthenticated && currentUser.Roles.Contains(Roles.Admin);
}
