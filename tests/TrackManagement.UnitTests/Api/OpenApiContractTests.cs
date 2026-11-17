using System.Text.Json;
using System.Text.Json.Nodes;

namespace TrackManagement.UnitTests.Api;

/// <summary>
/// The committed contract (specs/&lt;feature&gt;/contracts/openapi.json) is what the frontend generates
/// its types from, so it must match the document the API actually serves.
/// Update it with: ./scripts/export-contract.ps1 -Feature &lt;feature-folder&gt;
/// (sets UPDATE_CONTRACT=1 and CONTRACT_FEATURE, then runs this test).
/// </summary>
public sealed class OpenApiContractTests
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public async Task ServedDocument_MatchesCommittedContract()
    {
        // Arrange
        await using var factory = new TestApiFactory("Development");
        using var client = factory.CreateClient();
        var contractPath = ResolveContractPath();

        // Act
        var served = Normalize(await client.GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken));

        // Assert (or update)
        if (Environment.GetEnvironmentVariable("UPDATE_CONTRACT") == "1")
        {
            await File.WriteAllTextAsync(contractPath, served + "\n", TestContext.Current.CancellationToken);
            return;
        }

        File.Exists(contractPath).ShouldBeTrue($"Missing contract {contractPath}; run scripts/export-contract.ps1");
        var committed = Normalize(await File.ReadAllTextAsync(contractPath, TestContext.Current.CancellationToken));
        served.ShouldBe(committed, "API contract drifted. Run scripts/export-contract.ps1 and commit the result.");
    }

    [Fact]
    public async Task ServedDocument_ListsTrackValuesOnlyWhereTheyApply()
    {
        // Arrange
        await using var factory = new TestApiFactory("Development");
        using var client = factory.CreateClient();

        // Act
        var document = JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken))!;

        // Assert
        var schemas = document["components"]!["schemas"]!;
        string[] genres = ["Arabic Pop", "Mahraganat", "Khaleeji", "Hip-Hop", "Electronic", "Rap", "R&B", "Rock"];
        string[] statuses = ["draft", "submitted", "distributed"];
        Enum(schemas["CreateTrackRequest"]!["properties"]!["genre"]).ShouldBe(genres);
        Enum(schemas["TrackListItemDto"]!["properties"]!["genre"]).ShouldBe(genres);
        Enum(schemas["ChangeTrackStatusRequest"]!["properties"]!["status"]).ShouldBe(statuses);
        Enum(schemas["TrackStatus"]).ShouldBe(statuses);
        // A distribution's status is not a track status (its values arrive in Phase 4).
        Enum(schemas["TrackDistributionDto"]!["properties"]!["status"]).ShouldBeEmpty();

        var listParameters = document["paths"]!["/api/tracks"]!["get"]!["parameters"]!.AsArray();
        Enum(listParameters.Single(p => (string?)p!["name"] == "genre")!["schema"]).ShouldBe(genres);
        Enum(listParameters.Single(p => (string?)p!["name"] == "status")!["schema"]).ShouldBe(statuses);
    }

    private static string[] Enum(JsonNode? schema) =>
        schema?["enum"]?.AsArray().Select(v => (string)v!).ToArray() ?? [];

    private static string Normalize(string json)
    {
        var node = JsonNode.Parse(json)!;
        node.AsObject().Remove("servers"); // depends on the test host URL
        return node.ToJsonString(Indented).ReplaceLineEndings("\n");
    }

    private static string ResolveContractPath()
    {
        var repoRoot = FindRepoRoot();
        var specs = Path.Combine(repoRoot, "specs");
        var feature = Environment.GetEnvironmentVariable("CONTRACT_FEATURE")
            ?? Directory.GetDirectories(specs)
                .Select(Path.GetFileName)
                .Where(d => File.Exists(Path.Combine(specs, d!, "contracts", "openapi.json")))
                .OrderDescending(StringComparer.Ordinal)
                .First()!;
        return Path.Combine(specs, feature, "contracts", "openapi.json");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TrackManagement.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root (TrackManagement.slnx) not found.");
    }
}
