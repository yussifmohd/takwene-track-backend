using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;

namespace TrackManagement.Api.Security;

public sealed class ApiOptions
{
    public const string SectionName = "Api";

    /// <summary>Largest accepted request body. Every request in this API is small JSON.</summary>
    [Range(1, 10 * 1024 * 1024)]
    public long MaxRequestBodyBytes { get; init; } = 32 * 1024;
}

public static class RequestBodyLimit
{
    public static IServiceCollection AddRequestBodyLimit(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ApiOptions>()
            .Bind(configuration.GetSection(ApiOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Kestrel enforces it while reading (also for chunked bodies without Content-Length).
        services.AddOptions<KestrelServerOptions>()
            .Configure<IOptions<ApiOptions>>((kestrel, api) => kestrel.Limits.MaxRequestBodySize = api.Value.MaxRequestBodyBytes);
        return services;
    }

    /// <summary>Refuses a declared oversize body before anything reads it (413 ProblemDetails).</summary>
    public static IApplicationBuilder UseRequestBodyLimit(this IApplicationBuilder app)
    {
        var limit = app.ApplicationServices.GetRequiredService<IOptions<ApiOptions>>().Value.MaxRequestBodyBytes;
        return app.Use(async (context, next) =>
        {
            if (context.Request.ContentLength > limit)
            {
                throw new Microsoft.AspNetCore.Http.BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge);
            }

            await next(context);
        });
    }
}
