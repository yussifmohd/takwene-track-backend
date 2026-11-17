using MediatR;
using Microsoft.AspNetCore.Mvc;
using TrackManagement.Api.Extensions;
using TrackManagement.Api.Security;
using TrackManagement.Application.Features.Tracks;
using TrackManagement.Application.Features.Tracks.Commands.ChangeTrackStatus;
using TrackManagement.Application.Features.Tracks.Commands.CreateTrack;
using TrackManagement.Application.Features.Tracks.Queries.GetTrack;
using TrackManagement.Application.Features.Tracks.Queries.ListTracks;
using PagedTracks = TrackManagement.Application.Common.Models.PagedResult<TrackManagement.Application.Features.Tracks.TrackListItemDto>;

namespace TrackManagement.Api.Endpoints;

/// <summary>Create-track body. All fields required; strings so bad values are reported per field.</summary>
/// <param name="ReleaseDate">Calendar date, "YYYY-MM-DD".</param>
public sealed record CreateTrackRequest(string? Title, Guid? ArtistId, string? Isrc, string? Genre, string? ReleaseDate);

/// <summary>Status-change body: the wanted status ("submitted", "draft" or "distributed").</summary>
public sealed record ChangeTrackStatusRequest(string? Status);

/// <summary>List query string. All filters optional and combined with AND.</summary>
public sealed record ListTracksRequest(
    [FromQuery(Name = "page")] int? Page,
    [FromQuery(Name = "pageSize")] int? PageSize,
    [FromQuery(Name = "artistId")] Guid? ArtistId,
    [FromQuery(Name = "genre")] string? Genre,
    [FromQuery(Name = "status")] string? Status);

public static class TrackEndpoints
{
    public static IEndpointRouteBuilder MapTrackEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tracks").WithTags("Tracks");

        group.MapGet("/", async ([AsParameters] ListTracksRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var query = new ListTracksQuery(
                    request.Page ?? 1, request.PageSize ?? PagedTracks.DefaultPageSize, request.ArtistId, request.Genre, request.Status);
                return (await sender.Send(query, cancellationToken)).ToHttpResult();
            })
            .WithName("ListTracks")
            .WithSummary("List tracks, newest release first, filtered by artist, genre and/or status")
            .AllowAnonymous()
            .Produces<PagedTracks>()
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new GetTrackQuery(id), cancellationToken)).ToHttpResult())
            .WithName("GetTrack")
            .WithSummary("Get one track with its allowed next statuses and distributions")
            .AllowAnonymous()
            .Produces<TrackDetailDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (CreateTrackRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var command = new CreateTrackCommand(
                    request.Title ?? string.Empty,
                    request.ArtistId ?? Guid.Empty,
                    request.Isrc ?? string.Empty,
                    request.Genre ?? string.Empty,
                    request.ReleaseDate ?? string.Empty);
                return (await sender.Send(command, cancellationToken)).ToCreatedAtHttpResult(track => $"/api/tracks/{track.Id}");
            })
            .WithName("CreateTrack")
            .WithSummary("Add a track as a draft (admin only)")
            .RequireAdmin()
            .Produces<TrackDetailDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesValidationProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge);

        group.MapPatch("/{id:guid}/status", async (Guid id, ChangeTrackStatusRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new ChangeTrackStatusCommand(id, request.Status ?? string.Empty), cancellationToken)).ToHttpResult())
            .WithName("ChangeTrackStatus")
            .WithSummary("Change a track's status: draft → submitted, submitted → draft or distributed (admin only)")
            .RequireAdmin()
            .Produces<TrackDetailDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
