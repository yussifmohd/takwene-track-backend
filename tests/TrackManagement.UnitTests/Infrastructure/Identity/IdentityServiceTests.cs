using Ardalis.Result;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrackManagement.Application.Common.Security;
using TrackManagement.Application.Features.Auth;
using TrackManagement.Infrastructure.Identity;
using TrackManagement.UnitTests.TestSupport;

namespace TrackManagement.UnitTests.Infrastructure.Identity;

public sealed class IdentityServiceTests : IAsyncLifetime
{
    private const string Email = "admin@takwene.local";
    private const string Password = "Adm1n!Passw0rd#2026";

    private readonly ListLoggerProvider _logs = new();
    private readonly CountingPasswordHasher _hasher = new();
    private ServiceProvider _services = null!;
    private AsyncServiceScope _scope;
    private ApplicationUser _admin = null!;

    private IIdentityService Sut => _scope.ServiceProvider.GetRequiredService<IIdentityService>();

    public async ValueTask InitializeAsync()
    {
        _services = TestIdentity.Build(_logs, s => s.AddSingleton<IPasswordHasher<ApplicationUser>>(_hasher));
        _scope = _services.CreateAsyncScope();
        _admin = await TestIdentity.CreateUserAsync(_scope.ServiceProvider, Email, Password, "Admin");
    }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _services.DisposeAsync();
    }

    [Fact]
    public async Task CorrectPassword_ReturnsUserWithRoles_AndLogsSuccessWithoutEmail()
    {
        var result = await Sut.AuthenticateAsync(Email, Password, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(_admin.Id);
        result.Value.Roles.ShouldBe(["Admin"]);
        _logs.Entries.ShouldContain(e => e.EventId == 1001 && e.Message.Contains(_admin.Id.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task EmailLookup_IgnoresCase()
    {
        var result = await Sut.AuthenticateAsync("ADMIN@Takwene.Local", Password, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task UnknownEmail_RunsDummyHashVerification_AndReturnsGenericError()
    {
        _hasher.ResetCalls();

        var result = await Sut.AuthenticateAsync("nobody@takwene.local", Password, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ResultStatus.Unauthorized);
        result.Errors.ShouldBe([AuthErrors.InvalidCredentials]);
        _hasher.VerifyCalls.ShouldBe(1); // same work as a wrong password → no timing difference
        _logs.Entries.ShouldContain(e => e.EventId == 1002 && e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task WrongPassword_ReturnsTheSameGenericError_AndLogsReason()
    {
        var result = await Sut.AuthenticateAsync(Email, "Wrong!Passw0rd#1", TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ResultStatus.Unauthorized);
        result.Errors.ShouldBe([AuthErrors.InvalidCredentials]);
        _logs.Entries.ShouldContain(e => e.EventId == 1003);
    }

    [Fact]
    public async Task FifthWrongPassword_LocksOut_ThenCorrectPasswordGetsTheSameGenericError()
    {
        for (var i = 0; i < IdentitySetup.MaxFailedAccessAttempts; i++)
        {
            await Sut.AuthenticateAsync(Email, "Wrong!Passw0rd#1", TestContext.Current.CancellationToken);
        }

        var result = await Sut.AuthenticateAsync(Email, Password, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ResultStatus.Unauthorized);
        result.Errors.ShouldBe([AuthErrors.InvalidCredentials]);
        _logs.Entries.ShouldContain(e => e.EventId == 1005); // lockout started
        _logs.Entries.ShouldContain(e => e.EventId == 1004); // refused while locked
        var users = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        (await users.GetLockoutEndDateAsync(await users.FindByEmailAsync(Email) ?? throw new InvalidOperationException()))
            .ShouldNotBeNull().ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(14));
    }

    [Fact]
    public async Task Logs_MaskTheEmail_AndNeverContainPasswordOrFullEmail()
    {
        await Sut.AuthenticateAsync("nobody@takwene.local", "Secret!Passw0rd#9", TestContext.Current.CancellationToken);
        await Sut.AuthenticateAsync(Email, "Secret!Passw0rd#9", TestContext.Current.CancellationToken);
        await Sut.AuthenticateAsync(Email, Password, TestContext.Current.CancellationToken);

        var text = string.Join("\n", _logs.Entries.Select(e => e.Message));
        text.ShouldContain("n***@takwene.local");
        text.ShouldContain("a***@takwene.local");
        text.ShouldNotContain("nobody@takwene.local");
        text.ShouldNotContain(Email);
        text.ShouldNotContain("Secret!Passw0rd#9");
        text.ShouldNotContain(Password);
    }

    [Theory]
    [InlineData("admin@takwene.local", "a***@takwene.local")]
    [InlineData("x@y.z", "x***@y.z")]
    [InlineData("no-at-sign", "***")]
    [InlineData("@leading.at", "***")]
    public void MaskEmail_KeepsFirstCharacterAndDomain(string email, string expected) =>
        IdentityService.MaskEmail(email).ShouldBe(expected);
}
