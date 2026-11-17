using Ardalis.Result;
using FluentValidation;
using TrackManagement.Application.Common.Messaging;
using TrackManagement.Application.Common.Persistence;
using TrackManagement.Application.Features.Dsps;
using TrackManagement.Application.Features.Tracks.Commands.ChangeTrackStatus;
using TrackManagement.Application.Features.Tracks.Specifications;
using TrackManagement.Domain.Dsps;
using TrackManagement.Domain.Tracks;

namespace TrackManagement.Application.Features.Tracks.Commands.RecordDistributionAnswer;

/// <param name="Status">The platform's simulated answer as sent: "live" or "rejected".</param>
public sealed record RecordDistributionAnswerCommand(Guid TrackId, Guid DspId, string Status) : ICommand<Result<TrackDetailDto>>;

public sealed class RecordDistributionAnswerCommandValidator : AbstractValidator<RecordDistributionAnswerCommand>
{
    public const string StatusMessage = "Status must be live or rejected.";

    public RecordDistributionAnswerCommandValidator() =>
        RuleFor(x => x.Status)
            .Must(s => DistributionStatusNames.Answers.Contains(s))
            .WithMessage(StatusMessage);
}

/// <summary>
/// Records a platform's simulated answer to a pending delivery. The track decides
/// (<see cref="DistributionStatusRules"/>); a refusal is a 409 with its reason. The track's status never changes.
/// </summary>
public sealed class RecordDistributionAnswerCommandHandler(IRepository<Track> tracks, IReadRepository<Dsp> dsps, TimeProvider timeProvider)
    : ICommandHandler<RecordDistributionAnswerCommand, Result<TrackDetailDto>>
{
    public const string NotSentMessage = "This track has not been sent to this platform.";

    public async Task<Result<TrackDetailDto>> Handle(RecordDistributionAnswerCommand request, CancellationToken cancellationToken)
    {
        var track = await tracks.FirstOrDefaultAsync(new TrackForUpdateSpec(request.TrackId), cancellationToken);
        if (track is null)
        {
            return Result.NotFound(ChangeTrackStatusCommandHandler.NotFoundMessage);
        }

        var answer = DistributionStatusNames.Parse(request.Status);
        var change = track.RecordAnswer(request.DspId, answer, timeProvider.GetUtcNow().UtcDateTime);
        if (change is null)
        {
            return Result.NotFound(NotSentMessage);
        }

        if (!change.Value.IsChanged)
        {
            return Result.Conflict(change.Value.Message!);
        }

        var dspNames = await DspNames.LoadAsync(dsps, cancellationToken);
        return Result.Success(TrackDetailDto.From(track, track.Artist.Name, dspNames));
    }
}
