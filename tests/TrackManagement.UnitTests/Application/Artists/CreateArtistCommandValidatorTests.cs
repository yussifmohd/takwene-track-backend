using Ardalis.Specification;
using TrackManagement.Application.Common.Persistence;
using TrackManagement.Application.Common.Validation;
using TrackManagement.Application.Features.Artists.Commands.CreateArtist;
using TrackManagement.Application.Features.Artists.Specifications;
using TrackManagement.Domain.Artists;

namespace TrackManagement.UnitTests.Application.Artists;

public sealed class CreateArtistCommandValidatorTests
{
    private readonly IReadRepository<Artist> _artists = Substitute.For<IReadRepository<Artist>>();
    private readonly CreateArtistCommandValidator _sut;

    public CreateArtistCommandValidatorTests() => _sut = new CreateArtistCommandValidator(_artists);

    private static CreateArtistCommand Valid(string? name = null, string? country = null, string? email = null) =>
        new(name ?? "Amr Diab", country ?? "EG", email ?? "booking@example.com");

    [Fact]
    public async Task Validate_ValidCommand_NoErrors()
    {
        var result = await _sut.ValidateAsync(Valid(), TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("", "Name", "Name is required.")]
    [InlineData("   ", "Name", "Name is required.")]
    public async Task Validate_MissingName_Fails(string name, string property, string message)
    {
        var result = await _sut.ValidateAsync(Valid(name: name), TestContext.Current.CancellationToken);
        result.Errors.ShouldContain(e => e.PropertyName == property && e.ErrorMessage == message);
    }

    [Fact]
    public async Task Validate_NameLength_CountsCodePoints()
    {
        // 200 emoji = 400 UTF-16 units but 200 characters: accepted; 201 refused.
        var ok = await _sut.ValidateAsync(Valid(name: string.Concat(Enumerable.Repeat("🎤", 200))), TestContext.Current.CancellationToken);
        var tooLong = await _sut.ValidateAsync(Valid(name: new string('ع', 201)), TestContext.Current.CancellationToken);

        ok.IsValid.ShouldBeTrue();
        tooLong.Errors.ShouldContain(e => e.PropertyName == "Name" && e.ErrorMessage == "Name must be at most 200 characters.");
    }

    [Theory]
    [InlineData("", "Country is required.")]
    [InlineData("XX", "Country must be an ISO 3166-1 alpha-2 code, for example EG or SA.")]
    [InlineData("UK", "Country must be an ISO 3166-1 alpha-2 code, for example EG or SA.")]
    [InlineData("EGY", "Country must be an ISO 3166-1 alpha-2 code, for example EG or SA.")]
    public async Task Validate_BadCountry_Fails(string country, string message)
    {
        var result = await _sut.ValidateAsync(Valid(country: country), TestContext.Current.CancellationToken);
        result.Errors.ShouldContain(e => e.PropertyName == "CountryCode" && e.ErrorMessage == message);
    }

    [Theory]
    [InlineData("", "Email is required.")]
    [InlineData("  ", "Email is required.")]
    [InlineData("not-an-email", "Email is not a valid email address.")]
    public async Task Validate_BadEmail_Fails(string email, string message)
    {
        var result = await _sut.ValidateAsync(Valid(email: email), TestContext.Current.CancellationToken);
        result.Errors.ShouldContain(e => e.PropertyName == "Email" && e.ErrorMessage == message);
    }

    [Fact]
    public async Task Validate_EmailLongerThan254_Fails()
    {
        var email = new string('a', 250) + "@x.eg";
        var result = await _sut.ValidateAsync(Valid(email: email), TestContext.Current.CancellationToken);
        result.Errors.ShouldContain(e => e.PropertyName == "Email" && e.ErrorMessage == "Email must be at most 254 characters.");
    }

    [Fact]
    public async Task Validate_DuplicateEmailIgnoringCaseAndSpaces_FailsWithConflictCode()
    {
        // Arrange: the repository holds "BOOKING@EXAMPLE.COM" (normalized)
        _artists.AnyAsync(Arg.Any<ISpecification<Artist>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((ArtistByNormalizedEmailSpec)ci[0]).Evaluate([Artist.Create("X", "EG", "booking@example.com", DateTime.UtcNow)]).Any());

        // Act
        var result = await _sut.ValidateAsync(Valid(email: "  Booking@Example.COM "), TestContext.Current.CancellationToken);

        // Assert
        var error = result.Errors.ShouldHaveSingleItem();
        error.PropertyName.ShouldBe("Email");
        error.ErrorMessage.ShouldBe(CreateArtistCommandValidator.DuplicateEmailMessage);
        error.ErrorCode.ShouldBe(ValidationErrorCodes.Conflict);
    }
}
