using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using TrackManagement.Application.Features.Auth;
using TrackManagement.Infrastructure.Identity;

namespace TrackManagement.UnitTests.Api.Auth;

public sealed class LoginEndpointTests
{
    private const string WrongPassword = "Wrong!Passw0rd#1";

    [Fact]
    public async Task ValidCredentials_Return200WithTokenTypeExpiryAndUser()
    {
        // Arrange
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();
        var before = DateTimeOffset.UtcNow;

        // Act
        var response = await AuthApi.LoginAsync(client, TestApiFactory.AdminEmail, TestApiFactory.AdminPassword);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = await AuthApi.ReadJsonAsync(response);
        var root = json.RootElement;
        root.GetProperty("accessToken").GetString().ShouldNotBeNullOrWhiteSpace();
        root.GetProperty("tokenType").GetString().ShouldBe("Bearer");
        var expiresAt = root.GetProperty("expiresAt").GetDateTimeOffset();
        expiresAt.ShouldBeInRange(before.AddMinutes(59), before.AddMinutes(61));
        root.GetProperty("user").GetProperty("email").GetString().ShouldBe(TestApiFactory.AdminEmail);
        root.GetProperty("user").GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ShouldBe(["Admin"]);
    }

    [Fact]
    public async Task EmailCaseAndSurroundingSpaces_AreIgnored()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var response = await AuthApi.LoginAsync(client, "  " + TestApiFactory.AdminEmail.ToUpperInvariant() + " ", TestApiFactory.AdminPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("nobody@takwene.test", TestApiFactory.AdminPassword)]
    [InlineData(TestApiFactory.AdminEmail, WrongPassword)]
    public async Task UnknownEmailOrWrongPassword_ReturnIdentical401Problem(string email, string password)
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var response = await AuthApi.LoginAsync(client, email, password);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        using var json = await AuthApi.ReadJsonAsync(response);
        json.RootElement.GetProperty("detail").GetString().ShouldBe(AuthErrors.InvalidCredentials);
        json.RootElement.TryGetProperty("errors", out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData("", "x")]
    [InlineData("not-an-email", "x")]
    [InlineData(TestApiFactory.AdminEmail, "")]
    public async Task InvalidBody_Returns400ValidationProblem(string email, string password)
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var response = await AuthApi.LoginAsync(client, email, password);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var json = await AuthApi.ReadJsonAsync(response);
        json.RootElement.GetProperty("errors").EnumerateObject().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task FiveFailures_LockTheAccount_CorrectPasswordThenGetsTheSameGeneric401()
    {
        // Arrange
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();
        for (var i = 0; i < IdentitySetup.MaxFailedAccessAttempts; i++)
        {
            (await AuthApi.LoginAsync(client, TestApiFactory.AdminEmail, WrongPassword)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        // Act
        var response = await AuthApi.LoginAsync(client, TestApiFactory.AdminEmail, TestApiFactory.AdminPassword);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        using var json = await AuthApi.ReadJsonAsync(response);
        json.RootElement.GetProperty("detail").GetString().ShouldBe(AuthErrors.InvalidCredentials);
    }

    [Fact]
    public async Task AfterLockoutEnds_CorrectPasswordSignsIn()
    {
        // Arrange: lock out, then move the lockout end into the past (Identity reads the system clock).
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();
        for (var i = 0; i < IdentitySetup.MaxFailedAccessAttempts; i++)
        {
            await AuthApi.LoginAsync(client, TestApiFactory.AdminEmail, WrongPassword);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var admin = (await users.FindByEmailAsync(TestApiFactory.AdminEmail))!;
            await users.SetLockoutEndDateAsync(admin, DateTimeOffset.UtcNow.AddSeconds(-1));
        }

        // Act
        var response = await AuthApi.LoginAsync(client, TestApiFactory.AdminEmail, TestApiFactory.AdminPassword);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SuccessfulSignIn_ResetsTheFailureCount()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();
        for (var i = 0; i < IdentitySetup.MaxFailedAccessAttempts - 1; i++)
        {
            await AuthApi.LoginAsync(client, TestApiFactory.AdminEmail, WrongPassword);
        }

        (await AuthApi.LoginAsync(client, TestApiFactory.AdminEmail, TestApiFactory.AdminPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
        for (var i = 0; i < IdentitySetup.MaxFailedAccessAttempts - 1; i++)
        {
            await AuthApi.LoginAsync(client, TestApiFactory.AdminEmail, WrongPassword);
        }

        (await AuthApi.LoginAsync(client, TestApiFactory.AdminEmail, TestApiFactory.AdminPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FailedSignIn_LogsNeitherPasswordNorFullEmail()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        await AuthApi.LoginAsync(client, "nobody@takwene.test", "Leaky!Passw0rd#42");
        await AuthApi.LoginAsync(client, TestApiFactory.AdminEmail, "Leaky!Passw0rd#42");

        var logs = string.Join("\n", factory.Logs.Lines);
        logs.ShouldContain("n***@takwene.test");
        logs.ShouldNotContain("Leaky!Passw0rd#42");
        logs.ShouldNotContain("nobody@takwene.test");
        logs.ShouldNotContain(TestApiFactory.AdminEmail);
        logs.ShouldNotContain(TestApiFactory.AdminPassword);
    }
}
