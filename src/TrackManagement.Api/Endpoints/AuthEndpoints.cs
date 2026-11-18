using MediatR;
using TrackManagement.Api.Extensions;
using TrackManagement.Api.Security;
using TrackManagement.Application.Features.Auth;
using TrackManagement.Application.Features.Auth.Commands.Login;
using TrackManagement.Application.Features.Auth.Queries.GetCurrentUser;

namespace TrackManagement.Api.Endpoints;

/// <summary>Sign-in body. Bound here (not onto the command type) to keep the contract explicit.</summary>
public sealed record LoginRequest(string Email, string Password);

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", async (LoginRequest request, ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new LoginCommand(request.Email ?? string.Empty, request.Password ?? string.Empty), cancellationToken)).ToHttpResult())
            .WithName("Login")
            .WithSummary("Sign in as the admin and receive a 60-minute access token")
            .AllowAnonymous()
            .RequireRateLimiting(ApiRateLimiting.LoginPolicy)
            .Produces<LoginResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/me", async (ISender sender, CancellationToken cancellationToken) =>
                (await sender.Send(new GetCurrentUserQuery(), cancellationToken)).ToHttpResult())
            .WithName("GetCurrentUser")
            .WithSummary("Get the signed-in user")
            .RequireAuthorization()
            .Produces<CurrentUserDto>();

        return app;
    }
}
