using Ardalis.Result;
using TrackManagement.Application.Common.Security;
using TrackManagement.Application.Features.Auth;
using TrackManagement.Application.Features.Auth.Commands.Login;

namespace TrackManagement.UnitTests.Application.Auth;

public sealed class LoginCommandHandlerTests
{
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly IJwtTokenService _tokens = Substitute.For<IJwtTokenService>();
    private readonly LoginCommandHandler _sut;

    public LoginCommandHandlerTests() => _sut = new LoginCommandHandler(_identity, _tokens);

    [Fact]
    public async Task ValidCredentials_ReturnTokenAndUser()
    {
        // Arrange
        var user = new AuthenticatedUser(Guid.NewGuid(), "admin@takwene.local", ["Admin"]);
        var expires = DateTimeOffset.UtcNow.AddMinutes(60);
        _identity.AuthenticateAsync("admin@takwene.local", "pw", Arg.Any<CancellationToken>()).Returns(Result.Success(user));
        _tokens.Create(user).Returns(new AccessToken("jwt", expires));

        // Act
        var result = await _sut.Handle(new LoginCommand("admin@takwene.local", "pw"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldBe("jwt");
        result.Value.TokenType.ShouldBe("Bearer");
        result.Value.ExpiresAt.ShouldBe(expires);
        result.Value.User.ShouldBe(new CurrentUserDto("admin@takwene.local", user.Roles, expires));
    }

    [Fact]
    public async Task Email_IsTrimmedBeforeLookup()
    {
        _identity.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<AuthenticatedUser>.Unauthorized(AuthErrors.InvalidCredentials));

        await _sut.Handle(new LoginCommand("  admin@takwene.local  ", "pw"), TestContext.Current.CancellationToken);

        await _identity.Received(1).AuthenticateAsync("admin@takwene.local", "pw", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(ResultStatus.Unauthorized)]
    [InlineData(ResultStatus.NotFound)]
    [InlineData(ResultStatus.Error)]
    public async Task AnyIdentityFailure_ReturnsGenericUnauthorized_AndIssuesNoToken(ResultStatus failure)
    {
        // Arrange
        Result<AuthenticatedUser> failed = failure switch
        {
            ResultStatus.NotFound => Result<AuthenticatedUser>.NotFound("user 42 not found"),
            ResultStatus.Error => Result<AuthenticatedUser>.Error("locked out"),
            _ => Result<AuthenticatedUser>.Unauthorized("wrong password"),
        };
        _identity.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(failed);

        // Act
        var result = await _sut.Handle(new LoginCommand("x@takwene.local", "pw"), TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(ResultStatus.Unauthorized);
        result.Errors.ShouldBe([AuthErrors.InvalidCredentials]);
        _tokens.DidNotReceive().Create(Arg.Any<AuthenticatedUser>());
    }
}
