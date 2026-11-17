namespace TrackManagement.Api.Endpoints;

public static class EndpointRouteBuilderExtensions
{
    /// <summary>Maps every feature's endpoint group. Add one line per feature.</summary>
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapHealthEndpoints();
        return app;
    }
}
