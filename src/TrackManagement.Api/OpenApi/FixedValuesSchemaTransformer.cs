using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using TrackManagement.Api.Endpoints;
using TrackManagement.Domain.Tracks;

namespace TrackManagement.Api.OpenApi;

/// <summary>
/// Genre and track status travel as strings (so unknown values are a 400 naming the field), but only
/// the fixed values are valid. List them as <c>enum</c> in the contract so clients get exact types.
/// Applied only to the request/query members below; a delivery answer's "status" has its own values.
/// </summary>
internal sealed class FixedValuesSchemaTransformer : IOpenApiSchemaTransformer
{
    private static readonly Type[] TrackRequestTypes = [typeof(CreateTrackRequest), typeof(ChangeTrackStatusRequest), typeof(ListTracksRequest)];

    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var (name, owner) = context.JsonPropertyInfo is { } property
            ? (property.Name, property.DeclaringType)
            : (context.ParameterDescription?.Name, context.ParameterDescription?.ModelMetadata?.ContainerType);
        if (owner is null || !TrackRequestTypes.Contains(owner) && owner != typeof(RecordDistributionAnswerRequest) && name != "genre")
        {
            return Task.CompletedTask;
        }

        var values = name switch
        {
            "genre" => Genres.All,
            "status" when owner == typeof(RecordDistributionAnswerRequest) => DistributionStatusNames.Answers,
            "status" => TrackStatusNames.All,
            _ => null,
        };
        if (values is not null && schema.Enum is not { Count: > 0 })
        {
            schema.Enum = values.Select(v => (JsonNode)JsonValue.Create(v)).ToList();
        }

        return Task.CompletedTask;
    }
}
