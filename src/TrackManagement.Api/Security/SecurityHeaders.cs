namespace TrackManagement.Api.Security;

/// <summary>
/// Headers on every API response, errors included (set when the response starts):
/// no MIME sniffing, no framing, no referrer, a content policy that allows nothing (JSON needs
/// nothing), and <c>no-store</c> on <c>/api</c> so tokens and admin-only emails are never cached.
/// The Development-only API docs (<c>/scalar</c>, <c>/openapi</c>) get no content policy, or their
/// page could not load its own scripts. No HSTS: the API is served over plain HTTP until a domain
/// and certificate exist (TLS ends at the load balancer when it does).
/// </summary>
public static class SecurityHeaders
{
    public const string ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

    private static readonly PathString[] DocsPaths = ["/scalar", "/openapi"];

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) => app.Use((context, next) =>
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            if (!DocsPaths.Any(p => context.Request.Path.StartsWithSegments(p)))
            {
                headers.ContentSecurityPolicy = ContentSecurityPolicy;
            }

            if (context.Request.Path.StartsWithSegments("/api"))
            {
                headers.CacheControl = "no-store";
            }

            return Task.CompletedTask;
        });
        return next(context);
    });
}
