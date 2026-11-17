using System.Text.Json;
using System.Text.Json.Serialization;
using TrackManagement.Api.ExceptionHandlers;
using TrackManagement.Api.OpenApi;

namespace TrackManagement.Api;

public static class ApiServiceCollectionExtensions
{
    public const string CorsPolicyName = "WebApp";

    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);

        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        });

        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = ctx =>
            {
                ctx.ProblemDetails.Instance ??= ctx.HttpContext.Request.Path;
                ctx.ProblemDetails.Extensions["traceId"] = System.Diagnostics.Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier;
            });
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddOpenApi("v1", options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options => options.AddPolicy(CorsPolicyName, policy => policy
            .WithOrigins(allowedOrigins)
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
            .WithHeaders("Authorization", "Content-Type")));

        return services;
    }
}
