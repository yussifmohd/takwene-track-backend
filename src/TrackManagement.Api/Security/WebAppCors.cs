using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace TrackManagement.Api.Security;

public sealed class WebAppCorsOptions
{
    public const string SectionName = "Cors";

    /// <summary>Exact origins of the web app (scheme://host[:port]); empty = no browser origin is allowed.</summary>
    public string[] AllowedOrigins { get; init; } = [];
}

/// <summary>
/// Browser access only from an explicit list of origins, checked at startup: each must be an absolute
/// http(s) origin without path, query or wildcard. Only the methods and headers the web app uses.
/// </summary>
public static class WebAppCors
{
    public const string PolicyName = "WebApp";

    public static readonly string[] Methods = ["GET", "POST", "PATCH"];

    public static IServiceCollection AddWebAppCors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<WebAppCorsOptions>()
            .Bind(configuration.GetSection(WebAppCorsOptions.SectionName))
            .Validate(o => o.AllowedOrigins.All(IsOrigin),
                "Cors:AllowedOrigins must contain only absolute http(s) origins such as https://app.example.com (no '*', path or query).")
            .ValidateOnStart();

        services.AddCors();
        services.AddOptions<CorsOptions>().Configure<IOptions<WebAppCorsOptions>>((cors, settings) =>
            cors.AddPolicy(PolicyName, policy => policy
                .WithOrigins(settings.Value.AllowedOrigins)
                .WithMethods(Methods)
                .WithHeaders("Authorization", "Content-Type")));
        return services;
    }

    public static bool IsOrigin(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && !value.Contains('*', StringComparison.Ordinal)
        && Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && string.IsNullOrEmpty(uri.UserInfo)
        && uri.AbsolutePath == "/"
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment)
        && !value.TrimEnd().EndsWith('/');
}
