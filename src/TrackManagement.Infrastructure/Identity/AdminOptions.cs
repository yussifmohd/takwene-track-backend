namespace TrackManagement.Infrastructure.Identity;

/// <summary>The single admin account, provisioned on startup. Set via ADMIN__EMAIL / ADMIN__PASSWORD; never committed.</summary>
public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}
