using System.Net;
using TrackManagement.UnitTests.TestSupport;

namespace TrackManagement.UnitTests.Api.Tracks;

public sealed class ChangeTrackStatusEndpointTests : TracksApiTestBase
{
    private async Task<string> NewTrackAsync()
    {
        var artistId = await TracksApi.CreateArtistAsync(Client, AdminToken);
        return (await TracksApi.CreateTrackAsync(Client, AdminToken, artistId, "EGA1B2600001")).ToString();
    }

    private async Task<(HttpStatusCode Status, System.Text.Json.JsonDocument Body)> ChangeAsync(string id, string? status, string? token = null)
    {
        var response = await TracksApi.ChangeStatusAsync(Client, token ?? AdminToken, id, new { status });
        return (response.StatusCode, await TracksApi.JsonAsync(response));
    }

    [Fact]
    public async Task FullWalk_FollowsTheRules()
    {
        // Arrange
        var id = await NewTrackAsync();

        // draft → distributed is not allowed
        var (status, body) = await ChangeAsync(id, "distributed");
        status.ShouldBe(HttpStatusCode.Conflict);
        body.RootElement.GetProperty("detail").GetString().ShouldBe("A draft track must be submitted before it can be distributed.");

        // draft → draft is a conflict too
        (await ChangeAsync(id, "draft")).Status.ShouldBe(HttpStatusCode.Conflict);

        // draft → submitted → draft → submitted → distributed
        (status, body) = await ChangeAsync(id, "submitted");
        status.ShouldBe(HttpStatusCode.OK);
        body.RootElement.GetProperty("status").GetString().ShouldBe("submitted");
        body.RootElement.GetProperty("allowedNextStatuses").EnumerateArray().Select(s => s.GetString()).ShouldBe(["draft", "distributed"]);
        body.RootElement.GetProperty("updatedAtUtc").GetDateTime().ShouldBe(DateTime.UtcNow, TimeSpan.FromMinutes(1));

        (await ChangeAsync(id, "draft")).Status.ShouldBe(HttpStatusCode.OK);
        (await ChangeAsync(id, "submitted")).Status.ShouldBe(HttpStatusCode.OK);
        (status, body) = await ChangeAsync(id, "distributed");
        status.ShouldBe(HttpStatusCode.OK);
        body.RootElement.GetProperty("allowedNextStatuses").GetArrayLength().ShouldBe(0);

        // distributed is final
        foreach (var target in new[] { "draft", "submitted" })
        {
            (status, body) = await ChangeAsync(id, target);
            status.ShouldBe(HttpStatusCode.Conflict);
            body.RootElement.GetProperty("detail").GetString().ShouldBe("A distributed track is final; its status cannot change.");
        }

        // The stored state is what the responses said
        using var read = await TracksApi.JsonAsync(await TracksApi.GetAsync(Client, id));
        read.RootElement.GetProperty("status").GetString().ShouldBe("distributed");
    }

    [Theory]
    [InlineData("published", "Status must be one of: draft, submitted, distributed.")]
    [InlineData("Submitted", "Status must be one of: draft, submitted, distributed.")]
    [InlineData(null, "Status is required.")]
    public async Task UnknownOrMissingStatus_Returns400OnStatus(string? value, string message)
    {
        var id = await NewTrackAsync();

        var (status, body) = await ChangeAsync(id, value);

        status.ShouldBe(HttpStatusCode.BadRequest);
        body.RootElement.GetProperty("errors").GetProperty("status")[0].GetString().ShouldBe(message);
    }

    [Fact]
    public async Task UnknownTrack_Returns404()
    {
        var (status, _) = await ChangeAsync(Guid.NewGuid().ToString(), "submitted");
        status.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WithoutTokenOrAdminRole_IsRefused()
    {
        var id = await NewTrackAsync();

        (await TracksApi.ChangeStatusAsync(Client, null, id, new { status = "submitted" })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await TracksApi.ChangeStatusAsync(Client, TestTokens.Create(roles: []), id, new { status = "submitted" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
