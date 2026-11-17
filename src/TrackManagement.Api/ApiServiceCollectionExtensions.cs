using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrackManagement.Api.ExceptionHandlers;
using TrackManagement.Api.OpenApi;
using TrackManagement.Api.Security;
using TrackManagement.Application.Common.Security;

namespace TrackManagement.Api;

public static class ApiServiceCollectionExtensions
{
    public const string CorsPolicyName = "WebApp";

    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            // Numbers must be JSON numbers (web defaults also accept "42"), keeping the contract precise.
            options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        });

        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = ctx =>
            {
                ctx.ProblemDetails.Instance ??= ctx.HttpContext.Request.Path;
                ctx.ProblemDetails.Extensions["traceId"] = System.Diagnostics.Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier;
            });
        // Order matters: the first handler that recognises the exception answers.
        services.AddExceptionHandler<BadRequestExceptionHandler>();
        services.AddExceptionHandler<UniqueConstraintExceptionHandler>();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddRequestBodyLimit(configuration);

        services.AddOpenApi("v1", options => options
            .AddDocumentTransformer<BearerSecuritySchemeTransformer>()
            .AddOperationTransformer<AuthorizeOperationTransformer>()
            .AddSchemaTransformer<OmittedWhenNullSchemaTransformer>());

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddAuthPolicies();
        services.AddLoginRateLimiting(configuration);
        services.AddTrustedForwardedHeaders(configuration);

        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options => options.AddPolicy(CorsPolicyName, policy => policy
            .WithOrigins(allowedOrigins)
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
            .WithHeaders("Authorization", "Content-Type")));

        return services;
    }
}
