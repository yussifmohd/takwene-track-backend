using Ardalis.Result;
using TrackManagement.Application.Common.Messaging;
using TrackManagement.Application.Common.Security;

namespace TrackManagement.Application.Features.Auth.Queries.GetCurrentUser;

public sealed record GetCurrentUserQuery : IQuery<Result<CurrentUserDto>>;

public sealed class GetCurrentUserQueryHandler(ICurrentUser currentUser) : IQueryHandler<GetCurrentUserQuery, Result<CurrentUserDto>>
{
    public Task<Result<CurrentUserDto>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.Email is null || currentUser.ExpiresAt is null)
        {
            return Task.FromResult(Result<CurrentUserDto>.Unauthorized());
        }

        return Task.FromResult(Result.Success(new CurrentUserDto(currentUser.Email, currentUser.Roles, currentUser.ExpiresAt.Value)));
    }
}
