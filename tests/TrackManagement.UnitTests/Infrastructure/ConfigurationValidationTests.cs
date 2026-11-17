using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TrackManagement.Infrastructure;
using TrackManagement.Infrastructure.Auth;
using TrackManagement.Infrastructure.Persistence;

namespace TrackManagement.UnitTests.Infrastructure;

/// <summary>The API must refuse to start without required secrets (spec FR-004).</summary>
public sealed class ConfigurationValidationTests
{
    private const string ValidKey = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void ValidConfiguration_PassesValidation()
    {
        using var provider = Build(new() { ["ConnectionStrings:Default"] = "Host=db", ["Jwt:SigningKey"] = ValidKey });

        provider.GetRequiredService<IOptions<JwtOptions>>().Value.SigningKey.ShouldBe(ValidKey);
        provider.GetRequiredService<IOptions<ConnectionStringOptions>>().Value.Default.ShouldBe("Host=db");
    }

    [Fact]
    public void MissingConnectionString_FailsValidation()
    {
        using var provider = Build(new() { ["Jwt:SigningKey"] = ValidKey });

        var ex = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<ConnectionStringOptions>>().Value);
        ex.Message.ShouldContain("ConnectionStrings:Default");
    }

    [Theory]
    [InlineData("")]
    [InlineData("too-short-key")]
    public void MissingOrShortSigningKey_FailsValidation(string key)
    {
        using var provider = Build(new() { ["ConnectionStrings:Default"] = "Host=db", ["Jwt:SigningKey"] = key });

        var ex = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<JwtOptions>>().Value);
        ex.Message.ShouldContain("SigningKey");
    }

    [Fact]
    public void DatabaseHealthCheck_HasShortTimeout_SoProbesFailFastWhenDatabaseIsDown()
    {
        using var provider = Build(new() { ["ConnectionStrings:Default"] = "Host=db", ["Jwt:SigningKey"] = ValidKey });

        var registration = provider.GetRequiredService<IOptions<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckServiceOptions>>()
            .Value.Registrations.Single(r => r.Name == DependencyInjection.DatabaseHealthCheckName);

        registration.Timeout.ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(5));
    }

    private static ServiceProvider Build(Dictionary<string, string?> values)
    {
        var defaults = new Dictionary<string, string?> { ["Jwt:Issuer"] = "i", ["Jwt:Audience"] = "a" };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(defaults).AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider();
    }
}
