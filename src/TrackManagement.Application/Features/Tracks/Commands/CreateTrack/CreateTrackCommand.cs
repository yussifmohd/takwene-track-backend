using Ardalis.Result;
using FluentValidation;
using TrackManagement.Application.Common.Messaging;
using TrackManagement.Application.Common.Persistence;
using TrackManagement.Application.Common.Validation;
using TrackManagement.Application.Features.Artists.Specifications;
using TrackManagement.Application.Features.Tracks.Specifications;
using TrackManagement.Domain.Artists;
using TrackManagement.Domain.Tracks;

namespace TrackManagement.Application.Features.Tracks.Commands.CreateTrack;

/// <param name="ReleaseDate">"YYYY-MM-DD" as sent, so a bad value is reported on its field.</param>
public sealed record CreateTrackCommand(string Title, Guid ArtistId, string Isrc, string Genre, string ReleaseDate)
    : ICommand<Result<TrackDetailDto>>;

public sealed class CreateTrackCommandValidator : AbstractValidator<CreateTrackCommand>
{
    public const string DuplicateIsrcMessage = "Another track already uses this ISRC.";
    public const string UnknownArtistMessage = "Choose an existing artist.";
    public static readonly string IsrcFormatMessage = $"ISRC must look like {Domain.Tracks.Isrc.FormatHint}.";

    public CreateTrackCommandValidator(IReadRepository<Artist> artists, IReadRepository<Track> tracks)
    {
        RuleFor(x => x.Title)
            .Cascade(CascadeMode.Stop)
            .Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("Title is required.")
            .MaximumCodePoints(Track.TitleMaxLength).WithMessage($"Title must be at most {Track.TitleMaxLength} characters.");

        RuleFor(x => x.ArtistId)
            .Cascade(CascadeMode.Stop)
            .NotEqual(Guid.Empty).WithMessage("Artist is required.")
            .MustAsync(async (id, ct) => await artists.AnyAsync(new ArtistByIdSpec(id), ct)).WithMessage(UnknownArtistMessage);

        RuleFor(x => x.Isrc)
            .Cascade(CascadeMode.Stop)
            .Must(i => !string.IsNullOrWhiteSpace(i)).WithMessage("ISRC is required.")
            .Must(i => Domain.Tracks.Isrc.TryNormalize(i, out _)).WithMessage(IsrcFormatMessage)
            .MustAsync(async (isrc, ct) =>
                    !Domain.Tracks.Isrc.TryNormalize(isrc, out var normalized)
                    || !await tracks.AnyAsync(new TrackByIsrcSpec(normalized), ct))
                .WithMessage(DuplicateIsrcMessage)
                .WithErrorCode(ValidationErrorCodes.Conflict);

        RuleFor(x => x.Genre)
            .Cascade(CascadeMode.Stop)
            .Must(g => !string.IsNullOrWhiteSpace(g)).WithMessage("Genre is required.")
            .KnownGenre();

        RuleFor(x => x.ReleaseDate)
            .Cascade(CascadeMode.Stop)
            .Must(d => !string.IsNullOrWhiteSpace(d)).WithMessage("Release date is required.")
            .Must(d => TrackRules.TryParseReleaseDate(d, out _)).WithMessage("Release date must be a real date written as YYYY-MM-DD.")
            .Must(d => TrackRules.TryParseReleaseDate(d, out var date) && date >= Track.MinReleaseDate && date <= Track.MaxReleaseDate)
                .WithMessage("Release date must be between 1900-01-01 and 2100-12-31.");
    }
}

/// <summary>Adds the track as a draft; the Unit of Work behavior saves it (a unique index guards concurrent duplicate ISRCs).</summary>
public sealed class CreateTrackCommandHandler(IRepository<Track> tracks, IReadRepository<Artist> artists, TimeProvider timeProvider)
    : ICommandHandler<CreateTrackCommand, Result<TrackDetailDto>>
{
    public async Task<Result<TrackDetailDto>> Handle(CreateTrackCommand request, CancellationToken cancellationToken)
    {
        var artist = await artists.FirstOrDefaultAsync(new ArtistByIdSpec(request.ArtistId), cancellationToken);
        if (artist is null)
        {
            // Validated just before; only reachable if the artist vanished in between.
            return Result.Invalid(new ValidationError(nameof(CreateTrackCommand.ArtistId), CreateTrackCommandValidator.UnknownArtistMessage));
        }

        if (!TrackRules.TryParseReleaseDate(request.ReleaseDate, out var releaseDate))
        {
            // Unreachable after validation; refuse rather than store a default date.
            return Result.Invalid(new ValidationError(nameof(CreateTrackCommand.ReleaseDate), "Release date must be a real date written as YYYY-MM-DD."));
        }

        var track = Track.Create(request.Title, artist.Id, request.Isrc, request.Genre, releaseDate, timeProvider.GetUtcNow().UtcDateTime);
        await tracks.AddAsync(track, cancellationToken);
        return Result.Created(TrackDetailDto.From(track, artist.Name));
    }
}
