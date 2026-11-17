using IPNetwork = System.Net.IPNetwork;
using Microsoft.AspNetCore.HttpOverrides;

namespace TrackManagement.Api.Security;

/// <summary>
/// Trusted reverse-proxy ranges, configuration only (no hard-coded networks):
/// <c>ReverseProxy:KnownNetworks</c> = CIDR list (env <c>ReverseProxy__KnownNetworks__0..n</c>).
/// Compose sets the Docker private range; on AWS set the VPC CIDR of the load balancer.
/// Empty = trust loopback only. Headers from any other source are ignored (no IP spoofing).
/// </summary>
public sealed class ReverseProxyOptions
{
    public const string SectionName = "ReverseProxy";

    public List<string> KnownNetworks { get; init; } = [];
}

public static class ForwardedHeadersSetup
{
    public static IServiceCollection AddTrustedForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ReverseProxyOptions>()
            .Bind(configuration.GetSection(ReverseProxyOptions.SectionName))
            .Validate(o => o.KnownNetworks.TrueForAll(n => IPNetwork.TryParse(n, out _)),
                "ReverseProxy:KnownNetworks must contain CIDR ranges such as 10.0.0.0/16.")
            .ValidateOnStart();

        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<Microsoft.Extensions.Options.IOptions<ReverseProxyOptions>>((forwarded, proxy) =>
            {
                forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                forwarded.ForwardLimit = 1;
                foreach (var network in proxy.Value.KnownNetworks)
                {
                    if (IPNetwork.TryParse(network, out var parsed))
                    {
                        forwarded.KnownIPNetworks.Add(parsed);
                    }
                }
            });

        return services;
    }
}
