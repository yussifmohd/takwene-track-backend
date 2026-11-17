using Ardalis.Result;
using FluentValidation;
using TrackManagement.Application.Common.Messaging;
using TrackManagement.Application.Common.Persistence;
using TrackManagement.Application.Features.Dsps;
using TrackManagement.Application.Features.Tracks.Commands.ChangeTrackStatus;
using TrackManagement.Application.Features.Tracks.Specifications;
using TrackManagement.Domain.Dsps;
using TrackManagement.Domain.Tracks;

namespace TrackManagement.Application.Features.Tracks.Commands.DistributeTrack;

/// <param name="DspIds">Platforms to send to; repeats count once.</param>
public sealed record DistributeTrackCommand(Guid TrackId, IReadOnlyList<Guid>? DspIds) : ICommand<Result<TrackDetailDto>>;

public sealed class DistributeTrackCommandValidator : AbstractValidator<DistributeTrackCommand>
{
    /// <summary>A request limit, not a business rule: the number of platforms is data.</summary>
    public const int MaxDistinctPlatforms = 20;

    public const string RequiredMessage = "Choose at least one platform.";
    public static readonly string TooManyMessage = $"Send to at most {MaxDistinctPlatforms} different platforms at once.";

    public DistributeTrackCommandValidator() =>
        RuleFor(x => x.DspIds)
            .Cascade(CascadeMode.Stop)
            .Must(ids => ids is { Count: > 0 }).WithMessage(RequiredMessage)
            .Must(ids => ids!.Distinct().Count() <= MaxDistinctPlatforms).WithMessage(TooManyMessage);
}

/// <summary>
/// Sends the track to the platforms. Unknown platform ids are a 400 on <c>dspIds</c>; a draft track is a
/// 409; a request that changes nothing (all pending or live) still answers 200 with the track.
/// Two racing first deliveries to one platform collide on the primary key (409), and any change stamps
/// the track, so a race with a status change or an answer fails on the row version (409).
/// </summary>
public sealed class DistributeTrackCommandHandler(IRepository<Track> tracks, IReadRepository<Dsp> dsps, TimeProvider timeProvider)
    : ICommandHandler<DistributeTrackCommand, Result<TrackDetailDto>>
{
    public static string UnknownPlatformMessage(Guid id) => $"Unknown platform: {id}.";

    public async Task<Result<TrackDetailDto>> Handle(DistributeTrackCommand request, CancellationToken cancellationToken)
    {
        var track = await tracks.FirstOrDefaultAsync(new TrackForUpdateSpec(request.TrackId), cancellationToken);
        if (track is null)
        {
            return Result.NotFound(ChangeTrackStatusCommandHandler.NotFoundMessage);
        }

        var dspNames = await DspNames.LoadAsync(dsps, cancellationToken);
        var requested = request.DspIds!.Distinct().ToList();
        var unknown = requested.Where(id => !dspNames.ContainsKey(id)).ToList();
        if (unknown.Count > 0)
        {
            return Result.Invalid(unknown
                .Select(id => new ValidationError(nameof(DistributeTrackCommand.DspIds), UnknownPlatformMessage(id), null, ValidationSeverity.Error))
                .ToList());
        }

        var result = track.Distribute(requested, timeProvider.GetUtcNow().UtcDateTime);
        if (result.Outcome == DistributeOutcome.NotAllowed)
        {
            return Result.Conflict(result.Message!);
        }

        return Result.Success(TrackDetailDto.From(track, track.Artist.Name, dspNames));
    }
}
