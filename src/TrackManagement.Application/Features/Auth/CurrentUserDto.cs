namespace TrackManagement.Application.Features.Auth;

/// <summary>The signed-in user as seen by the API.</summary>
public sealed record CurrentUserDto(string Email, IReadOnlyList<string> Roles, DateTimeOffset ExpiresAt);
