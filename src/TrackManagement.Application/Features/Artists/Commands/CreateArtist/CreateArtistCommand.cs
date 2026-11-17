using Ardalis.Result;
using FluentValidation;
using TrackManagement.Application.Common.Messaging;
using TrackManagement.Application.Common.Persistence;
using TrackManagement.Application.Common.Validation;
using TrackManagement.Application.Features.Artists.Specifications;
using TrackManagement.Domain.Artists;
using TrackManagement.Domain.Countries;

namespace TrackManagement.Application.Features.Artists.Commands.CreateArtist;

public sealed record CreateArtistCommand(string Name, string CountryCode, string Email) : ICommand<Result<ArtistDto>>;

public sealed class CreateArtistCommandValidator : AbstractValidator<CreateArtistCommand>
{
    public const string DuplicateEmailMessage = "An artist with this email already exists.";

    public CreateArtistCommandValidator(IReadRepository<Artist> artists)
    {
        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Name is required.")
            .MaximumCodePoints(Artist.NameMaxLength).WithMessage($"Name must be at most {Artist.NameMaxLength} characters.");

        RuleFor(x => x.CountryCode)
            .Cascade(CascadeMode.Stop)
            .Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("Country is required.")
            .Must(CountryCodes.IsAssigned).WithMessage("Country must be an ISO 3166-1 alpha-2 code, for example EG or SA.");

        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .Must(e => !string.IsNullOrWhiteSpace(e)).WithMessage("Email is required.")
            .MaximumCodePoints(Artist.EmailMaxLength).WithMessage($"Email must be at most {Artist.EmailMaxLength} characters.")
            .Must(e => TextRules.IsEmail(e.Trim())).WithMessage("Email is not a valid email address.")
            .MustAsync(async (email, ct) => !await artists.AnyAsync(new ArtistByNormalizedEmailSpec(email), ct))
                .WithMessage(DuplicateEmailMessage)
                .WithErrorCode(ValidationErrorCodes.Conflict);
    }
}

/// <summary>Adds the artist; the Unit of Work behavior saves it (a unique index guards concurrent duplicates).</summary>
public sealed class CreateArtistCommandHandler(IRepository<Artist> artists, TimeProvider timeProvider)
    : ICommandHandler<CreateArtistCommand, Result<ArtistDto>>
{
    public async Task<Result<ArtistDto>> Handle(CreateArtistCommand request, CancellationToken cancellationToken)
    {
        var artist = Artist.Create(request.Name, request.CountryCode, request.Email, timeProvider.GetUtcNow().UtcDateTime);
        await artists.AddAsync(artist, cancellationToken);
        return Result.Created(new ArtistDto(artist.Id, artist.Name, artist.CountryCode, artist.Email, artist.CreatedAtUtc));
    }
}
