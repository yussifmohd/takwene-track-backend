using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrackManagement.Api;

namespace TrackManagement.UnitTests.Api.Security;

/// <summary>
/// The API's service registrations with the given settings, for checking option validation directly
/// (a host that fails to start surfaces only as a disposed-object error through WebApplicationFactory).
/// </summary>
internal static class ApiOptions
{
    public static ServiceProvider Build(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApi(configuration);
        return services.BuildServiceProvider();
    }
}
