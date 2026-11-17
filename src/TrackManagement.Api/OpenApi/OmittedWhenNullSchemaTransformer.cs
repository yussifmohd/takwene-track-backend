using System.Text.Json.Serialization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace TrackManagement.Api.OpenApi;

/// <summary>
/// A property marked <c>[JsonIgnore(Condition = WhenWritingNull)]</c> is left out of the JSON when
/// null (e.g. an artist's email for visitors), so the contract must not list it as required.
/// </summary>
internal sealed class OmittedWhenNullSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (schema.Required is not { Count: > 0 })
        {
            return Task.CompletedTask;
        }

        foreach (var property in context.JsonTypeInfo.Properties)
        {
            var omittedWhenNull = property.AttributeProvider?
                .GetCustomAttributes(typeof(JsonIgnoreAttribute), inherit: true)
                .OfType<JsonIgnoreAttribute>()
                .Any(a => a.Condition == JsonIgnoreCondition.WhenWritingNull) ?? false;
            if (omittedWhenNull)
            {
                schema.Required.Remove(property.Name);
            }
        }

        return Task.CompletedTask;
    }
}
