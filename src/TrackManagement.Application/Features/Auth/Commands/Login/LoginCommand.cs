using Ardalis.Result;
using TrackManagement.Application.Common.Messaging;

namespace TrackManagement.Application.Features.Auth.Commands.Login;

/// <summary>
/// A command (not a query): a failed attempt updates the account's failure counter and lockout.
/// </summary>
public sealed record LoginCommand(string Email, string Password) : ICommand<Result<LoginResponse>>;

public sealed record LoginResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAt, CurrentUserDto User);
