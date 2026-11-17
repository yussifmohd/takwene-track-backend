using TrackManagement.Application.Features.Auth.Commands.Login;

namespace TrackManagement.UnitTests.Application.Auth;

public sealed class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _sut = new();

    [Fact]
    public void ValidInput_Passes() =>
        _sut.Validate(new LoginCommand("admin@takwene.local", "secret")).IsValid.ShouldBeTrue();

    [Theory]
    [InlineData("", "Email is required.")]
    [InlineData("not-an-email", "Email is not a valid email address.")]
    [InlineData("a@b", "Email is not a valid email address.")]
    [InlineData("two@@takwene.local", "Email is not a valid email address.")]
    public void InvalidEmail_Fails(string email, string message)
    {
        var result = _sut.Validate(new LoginCommand(email, "secret"));

        result.Errors.ShouldContain(e => e.PropertyName == "Email" && e.ErrorMessage == message);
    }

    [Fact]
    public void TooLongEmail_Fails()
    {
        var email = new string('a', 250) + "@x.test";

        _sut.Validate(new LoginCommand(email, "secret")).Errors.ShouldContain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void EmailWithSurroundingSpaces_Passes() =>
        _sut.Validate(new LoginCommand("  admin@takwene.local ", "secret")).IsValid.ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void MissingPassword_Fails(string? password) =>
        _sut.Validate(new LoginCommand("admin@takwene.local", password!)).Errors
            .ShouldContain(e => e.PropertyName == "Password" && e.ErrorMessage == "Password is required.");

    [Fact]
    public void TooLongPassword_Fails() =>
        _sut.Validate(new LoginCommand("admin@takwene.local", new string('p', 129))).Errors
            .ShouldContain(e => e.PropertyName == "Password");
}
