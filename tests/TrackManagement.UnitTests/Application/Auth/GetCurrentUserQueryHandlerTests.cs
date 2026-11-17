using Ardalis.Result;
using TrackManagement.Application.Common.Security;
using TrackManagement.Application.Features.Auth.Queries.GetCurrentUser;

namespace TrackManagement.UnitTests.Application.Auth;

public sealed class GetCurrentUserQueryHandlerTests
{
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    [Fact]
    public async Task Authenticated_ReturnsEmailRolesAndExpiry()
    {
        var expires = DateTimeOffset.UtcNow.AddMinutes(30);
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.Email.Returns("admin@takwene.local");
        _currentUser.Roles.Returns(["Admin"]);
        _currentUser.ExpiresAt.Returns(expires);

        var result = await new GetCurrentUserQueryHandler(_currentUser).Handle(new GetCurrentUserQuery(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Email.ShouldBe("admin@takwene.local");
        result.Value.Roles.ShouldBe(["Admin"]);
        result.Value.ExpiresAt.ShouldBe(expires);
    }

    [Fact]
    public async Task NotAuthenticated_ReturnsUnauthorized()
    {
        _currentUser.IsAuthenticated.Returns(false);

        var result = await new GetCurrentUserQueryHandler(_currentUser).Handle(new GetCurrentUserQuery(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ResultStatus.Unauthorized);
    }
}
