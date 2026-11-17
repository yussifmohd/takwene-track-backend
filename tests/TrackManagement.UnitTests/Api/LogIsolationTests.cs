using TrackManagement.UnitTests.Api.Auth;

namespace TrackManagement.UnitTests.Api;

/// <summary>
/// Each in-process host logs into its own sinks. Before preserveStaticLogger, the host started last
/// took over the static Serilog logger and the others' logs went there (flaky log assertions).
/// </summary>
public sealed class LogIsolationTests
{
    [Fact]
    public async Task TwoHosts_KeepTheirLogsApart_AndRequestLoggingReachesTheOwnHost()
    {
        // Arrange: A starts first, B starts last (B would own the static logger)
        await using var first = new TestApiFactory();
        using var firstClient = first.CreateClient();
        await using var second = new TestApiFactory();
        using var secondClient = second.CreateClient();

        // Act
        await AuthApi.LoginAsync(firstClient, "only-first@takwene.test", "Wrong!Passw0rd#1");
        await secondClient.GetAsync("/api/dsps", TestContext.Current.CancellationToken);

        // Assert (the request log line is written as the pipeline unwinds, possibly after the response)
        for (var attempt = 0; attempt < 50 && !second.Logs.Lines.Any(IsDspsRequestLog); attempt++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        second.Logs.Lines.ShouldContain(l => IsDspsRequestLog(l));
        first.Logs.Lines.ShouldNotContain(l => IsDspsRequestLog(l));
        var firstLogs = string.Join("\n", first.Logs.Lines);
        var secondLogs = string.Join("\n", second.Logs.Lines);
        firstLogs.ShouldContain("o***@takwene.test");
        firstLogs.ShouldContain("/api/auth/login");
        firstLogs.ShouldNotContain("/api/dsps");
        secondLogs.ShouldContain("/api/dsps");
        secondLogs.ShouldNotContain("o***@takwene.test");
    }

    private static bool IsDspsRequestLog(string line) =>
        line.StartsWith("HTTP \"GET\" \"/api/dsps\" responded 200", StringComparison.Ordinal);
}
