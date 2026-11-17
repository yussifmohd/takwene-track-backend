using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using TrackManagement.Application.Common.Security;
using TrackManagement.Infrastructure.Auth;
using TrackManagement.UnitTests.TestSupport;

namespace TrackManagement.UnitTests.Infrastructure.Auth;

public sealed class JwtTokenServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 10, 0, 0, TimeSpan.Zero);

    private static readonly JwtOptions Settings = new()
    {
        Issuer = TestTokens.Issuer,
        Audience = TestTokens.Audience,
        SigningKey = TestTokens.SigningKey,
        ExpirationMinutes = 60,
    };

    private readonly JwtTokenService _sut = new(Options.Create(Settings), new FixedTimeProvider(Now));
    private readonly AuthenticatedUser _user = new(Guid.NewGuid(), "admin@takwene.local", ["Admin"]);

    [Fact]
    public void Create_ExpiresSixtyMinutesAfterIssue()
    {
        var token = _sut.Create(_user);

        token.ExpiresAt.ShouldBe(Now.AddMinutes(60));
        var jwt = new JsonWebToken(token.Token);
        jwt.ValidTo.ShouldBe(Now.AddMinutes(60).UtcDateTime);
        jwt.IssuedAt.ShouldBe(Now.UtcDateTime);
    }

    [Fact]
    public void Create_ContainsIdentityClaimsIssuerAudienceAndHs256()
    {
        var jwt = new JsonWebToken(_sut.Create(_user).Token);

        jwt.Alg.ShouldBe("HS256");
        jwt.Issuer.ShouldBe(TestTokens.Issuer);
        jwt.Audiences.ShouldBe([TestTokens.Audience]);
        jwt.Subject.ShouldBe(_user.Id.ToString());
        jwt.GetClaim("email").Value.ShouldBe("admin@takwene.local");
        jwt.Claims.Where(c => c.Type == "role").Select(c => c.Value).ShouldBe(["Admin"]);
        jwt.Id.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Create_ProducesTokenAcceptedByTheValidationRules()
    {
        // Validate "now" rather than the fixed clock: issue with a current clock.
        var sut = new JwtTokenService(Options.Create(Settings), TimeProvider.System);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(sut.Create(_user).Token, JwtValidation.CreateParameters(Settings));

        result.IsValid.ShouldBeTrue(result.Exception?.Message);
    }

    [Fact]
    public void ValidationRules_MatchTheAgreedPolicy()
    {
        var parameters = JwtValidation.CreateParameters(Settings);

        parameters.ValidateIssuer.ShouldBeTrue();
        parameters.ValidateAudience.ShouldBeTrue();
        parameters.ValidateLifetime.ShouldBeTrue();
        parameters.ValidateIssuerSigningKey.ShouldBeTrue();
        parameters.ValidAlgorithms.ShouldBe(["HS256"]);
        parameters.ClockSkew.ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(30));
    }
}
