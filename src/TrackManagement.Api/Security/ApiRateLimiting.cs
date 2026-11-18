using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using TrackManagement.Infrastructure.Auth;

namespace TrackManagement.Api.Security;

/// <summary>A fixed-window limit: <see cref="PermitLimit"/> requests per <see cref="WindowSeconds"/>.</summary>
public abstract class FixedWindowLimitOptions
{
    public int PermitLimit { get; init; }

    public int WindowSeconds { get; init; } = 60;

    internal FixedWindowRateLimiterOptions ToLimiterOptions() => new()
    {
        PermitLimit = PermitLimit,
        Window = TimeSpan.FromSeconds(WindowSeconds),
        QueueLimit = 0,
        AutoReplenishment = true,
    };
}

/// <summary>Sign-in attempts per client IP.</summary>
public sealed class LoginRateLimitOptions : FixedWindowLimitOptions
{
    public const string SectionName = "RateLimiting:Login";

    public LoginRateLimitOptions() => PermitLimit = 10;
}

/// <summary>All requests per client IP (health checks exempt).</summary>
public sealed class ApiRateLimitOptions : FixedWindowLimitOptions
{
    public const string SectionName = "RateLimiting:Api";

    public ApiRateLimitOptions() => PermitLimit = 300;
}

/// <summary>Change requests per signed-in user (per IP when no user is known).</summary>
public sealed class WritesRateLimitOptions : FixedWindowLimitOptions
{
    public const string SectionName = "RateLimiting:Writes";

    public WritesRateLimitOptions() => PermitLimit = 60;
}

/// <summary>
/// Three fixed-window limits, all answering 429 + <c>Retry-After</c> + ProblemDetails:
/// <list type="bullet">
/// <item>global, per client IP (after trusted forwarded headers) – stops floods, reads included;</item>
/// <item><see cref="LoginPolicy"/>, per client IP – slows password guessing before the credential check;</item>
/// <item><see cref="WritesPolicy"/>, per user id – a stolen token cannot bulk-change the catalogue.</item>
/// </list>
/// The middleware runs after authentication so the writes policy can see the user.
/// </summary>
public static class ApiRateLimiting
{
    public const string LoginPolicy = "login";
    public const string WritesPolicy = "writes";

    private static readonly PathString[] Exempt = ["/health", "/api/health"];

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        AddValidated<LoginRateLimitOptions>(services, configuration, LoginRateLimitOptions.SectionName);
        AddValidated<ApiRateLimitOptions>(services, configuration, ApiRateLimitOptions.SectionName);
        AddValidated<WritesRateLimitOptions>(services, configuration, WritesRateLimitOptions.SectionName);

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (Exempt.Any(path => context.Request.Path.StartsWithSegments(path)))
                {
                    return RateLimitPartition.GetNoLimiter("exempt");
                }

                var settings = context.RequestServices.GetRequiredService<IOptions<ApiRateLimitOptions>>().Value;
                return RateLimitPartition.GetFixedWindowLimiter(ClientIp(context), _ => settings.ToLimiterOptions());
            });

            limiter.AddPolicy(LoginPolicy, context =>
            {
                var settings = context.RequestServices.GetRequiredService<IOptions<LoginRateLimitOptions>>().Value;
                return RateLimitPartition.GetFixedWindowLimiter(ClientIp(context), _ => settings.ToLimiterOptions());
            });

            limiter.AddPolicy(WritesPolicy, context =>
            {
                var settings = context.RequestServices.GetRequiredService<IOptions<WritesRateLimitOptions>>().Value;
                var user = context.User.FindFirst(JwtClaims.Subject)?.Value;
                var key = user is null ? $"ip:{ClientIp(context)}" : $"user:{user}";
                return RateLimitPartition.GetFixedWindowLimiter(key, _ => settings.ToLimiterOptions());
            });

            limiter.OnRejected = WriteRejectionAsync;
        });

        return services;
    }

    /// <summary>Use on every change endpoint, together with the admin policy: <c>.RequireAdminWrite()</c>.</summary>
    public static TBuilder RequireAdminWrite<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAdmin().RequireRateLimiting(WritesPolicy);

    private static string ClientIp(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static void AddValidated<TOptions>(IServiceCollection services, IConfiguration configuration, string section)
        where TOptions : FixedWindowLimitOptions =>
        services.AddOptions<TOptions>()
            .Bind(configuration.GetSection(section))
            .Validate(o => o.PermitLimit > 0 && o.WindowSeconds > 0, $"{section} values must be positive.")
            .ValidateOnStart();

    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var http = context.HttpContext;
        int? retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var wait)
            ? (int)Math.Ceiling(wait.TotalSeconds)
            : null;
        if (retryAfter is not null)
        {
            http.Response.Headers.RetryAfter = retryAfter.Value.ToString(CultureInfo.InvariantCulture);
        }

        var what = http.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName == LoginPolicy
            ? "sign-in attempts"
            : "requests";
        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Type = "https://tools.ietf.org/html/rfc6585#section-4",
                Title = "Too many requests",
                Detail = retryAfter is null
                    ? $"Too many {what}. Try again later."
                    : $"Too many {what}. Try again in {retryAfter} seconds.",
            },
        });
    }
}
