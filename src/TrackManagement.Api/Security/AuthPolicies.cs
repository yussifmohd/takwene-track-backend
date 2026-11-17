using TrackManagement.Domain.Constants;

namespace TrackManagement.Api.Security;

public static class AuthPolicies
{
    /// <summary>Required by every endpoint that changes data (reads stay public).</summary>
    public const string Admin = "AdminOnly";

    public static IServiceCollection AddAuthPolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(Admin, policy => policy.RequireAuthenticatedUser().RequireRole(Roles.Admin));
        return services;
    }

    /// <summary>Use on every write endpoint or group: <c>group.MapPost(...).RequireAdmin()</c>.</summary>
    public static TBuilder RequireAdmin<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(Admin);
}
