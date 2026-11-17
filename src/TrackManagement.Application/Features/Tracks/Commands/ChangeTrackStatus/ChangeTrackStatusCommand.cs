using Ardalis.Result;
using FluentValidation;
using TrackManagement.Application.Common.Messaging;
using TrackManagement.Application.Common.Persistence;
using TrackManagement.Application.Features.Tracks.Specifications;
using TrackManagement.Domain.Tracks;

namespace TrackManagement.Application.Features.Tracks.Commands.ChangeTrackStatus;

/// <param name="Status">The wanted status as sent ("submitted"); unknown values fail validation (400).</param>
public sealed record ChangeTrackStatusCommand(Guid TrackId, string Status) : ICommand<Result<TrackDetailDto>>;

public sealed class ChangeTrackStatusCommandValidator : AbstractValidator<ChangeTrackStatusCommand>
{
    public ChangeTrackStatusCommandValidator() =>
        RuleFor(x => x.Status)
            .Cascade(CascadeMode.Stop)
            .Must(s => !string.IsNullOrWhiteSpace(s)).WithMessage("Status is required.")
            .KnownStatus();
}

/// <summary>
/// Asks the track to change; the track decides (<see cref="TrackStatusRules"/>). A refusal is a 409 with
/// the domain's explanation. A concurrent change to the same track fails on save (row version) → 409.
/// </summary>
public sealed class ChangeTrackStatusCommandHandler(IRepository<Track> tracks, TimeProvider timeProvider)
    : ICommandHandler<ChangeTrackStatusCommand, Result<TrackDetailDto>>
{
    public const string NotFoundMessage = "No track has this id.";

    public async Task<Result<TrackDetailDto>> Handle(ChangeTrackStatusCommand request, CancellationToken cancellationToken)
    {
        var track = await tracks.FirstOrDefaultAsync(new TrackForUpdateSpec(request.TrackId), cancellationToken);
        if (track is null)
        {
            return Result.NotFound(NotFoundMessage);
        }

        var target = TrackStatusNames.Parse(request.Status);
        var change = track.ChangeStatus(target, timeProvider.GetUtcNow().UtcDateTime);
        if (!change.IsChanged)
        {
            return Result.Conflict(change.Message!);
        }

        // The track is tracked: the Unit of Work behavior saves the change (no Update call, which would
        // mark every column as modified).
        return Result.Success(TrackDetailDto.From(track, track.Artist.Name));
    }
}
