using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace TrackManagement.Api.OpenApi;

/// <summary>Adds the JWT Bearer security scheme so the token can be entered once in Scalar.</summary>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public const string SchemeName = "Bearer";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "Takwene Track Management API",
            Version = "v1",
            Description = "Manage artists, tracks and their distribution to DSPs. Write operations require an Admin JWT.",
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "JWT access token from POST /api/auth/login.",
        };

        return Task.CompletedTask;
    }
}
