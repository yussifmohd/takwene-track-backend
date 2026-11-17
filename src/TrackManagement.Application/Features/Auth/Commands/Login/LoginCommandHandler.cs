using Ardalis.Result;
using TrackManagement.Application.Common.Messaging;
using TrackManagement.Application.Common.Security;

namespace TrackManagement.Application.Features.Auth.Commands.Login;

public sealed class LoginCommandHandler(IIdentityService identityService, IJwtTokenService tokenService)
    : ICommandHandler<LoginCommand, Result<LoginResponse>>
{
    public const string TokenType = "Bearer";

    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var authentication = await identityService.AuthenticateAsync(request.Email.Trim(), request.Password, cancellationToken);
        if (!authentication.IsSuccess)
        {
            // Whatever the reason, the caller only ever sees the generic message.
            return Result<LoginResponse>.Unauthorized(AuthErrors.InvalidCredentials);
        }

        var user = authentication.Value;
        var token = tokenService.Create(user);
        return Result.Success(new LoginResponse(
            token.Token,
            TokenType,
            token.ExpiresAt,
            new CurrentUserDto(user.Email, user.Roles, token.ExpiresAt)));
    }
}
