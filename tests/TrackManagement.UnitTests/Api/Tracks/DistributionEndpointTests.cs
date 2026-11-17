using System.Net;
using System.Text.Json;
using TrackManagement.Domain.Dsps;
using TrackManagement.UnitTests.TestSupport;

namespace TrackManagement.UnitTests.Api.Tracks;

public sealed class DistributionEndpointTests : TracksApiTestBase
{
    private static readonly Guid Spotify = Dsp.SpotifyId;
    private static readonly Guid Apple = Dsp.AppleMusicId;
    private static readonly Guid YouTube = Dsp.YouTubeMusicId;

    private async Task<string> NewTrackAsync(bool submit = true, string isrc = "EGA1B2600001")
    {
        var artistId = await TracksApi.CreateArtistAsync(Client, AdminToken);
        var id = (await TracksApi.CreateTrackAsync(Client, AdminToken, artistId, isrc)).ToString();
        if (submit)
        {
            (await TracksApi.ChangeStatusAsync(Client, AdminToken, id, new { status = "submitted" })).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        return id;
    }

    private async Task<(HttpStatusCode Status, JsonDocument Body)> DistributeAsync(string id, object body, string? token = null)
    {
        var response = await TracksApi.DistributeAsync(Client, token ?? AdminToken, id, body);
        return (response.StatusCode, await TracksApi.JsonAsync(response));
    }

    private async Task<(HttpStatusCode Status, JsonDocument Body)> AnswerAsync(string id, Guid dspId, string? status, string? token = null)
    {
        var response = await TracksApi.AnswerAsync(Client, token ?? AdminToken, id, dspId, new { status });
        return (response.StatusCode, await TracksApi.JsonAsync(response));
    }

    private static JsonElement Delivery(JsonDocument body, string name) =>
        body.RootElement.GetProperty("distributions").EnumerateArray().Single(d => d.GetProperty("dspName").GetString() == name);

    [Fact]
    public async Task Distribute_Submitted_SendsToThePlatforms_AndTheTrackBecomesDistributed()
    {
        var id = await NewTrackAsync();

        var (status, body) = await DistributeAsync(id, new { dspIds = new[] { Spotify, Apple, Spotify } });

        status.ShouldBe(HttpStatusCode.OK);
        body.RootElement.GetProperty("status").GetString().ShouldBe("distributed");
        body.RootElement.GetProperty("canDistribute").GetBoolean().ShouldBeTrue();
        var rows = body.RootElement.GetProperty("distributions").EnumerateArray().ToList();
        rows.Select(r => r.GetProperty("dspName").GetString()).ShouldBe(["Apple Music", "Spotify"]);
        foreach (var row in rows)
        {
            row.GetProperty("status").GetString().ShouldBe("pending");
            row.GetProperty("submittedAtUtc").GetDateTime().ShouldBe(DateTime.UtcNow, TimeSpan.FromMinutes(1));
            row.GetProperty("answeredAtUtc").ValueKind.ShouldBe(JsonValueKind.Null);
            row.GetProperty("allowedAnswers").EnumerateArray().Select(a => a.GetString()).ShouldBe(["live", "rejected"]);
        }

        // Anyone can read the deliveries
        using var read = await TracksApi.JsonAsync(await TracksApi.GetAsync(Client, id));
        read.RootElement.GetProperty("distributions").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task Distribute_Again_KeepsPendingAndLive_AddsNew_ResendsRejected()
    {
        var id = await NewTrackAsync();
        await DistributeAsync(id, new { dspIds = new[] { Spotify, Apple, YouTube } });
        (await AnswerAsync(id, Spotify, "live")).Status.ShouldBe(HttpStatusCode.OK);
        var (_, rejected) = await AnswerAsync(id, YouTube, "rejected");
        var firstSent = Delivery(rejected, "Apple Music").GetProperty("submittedAtUtc").GetDateTime();

        var (status, body) = await DistributeAsync(id, new { dspIds = new[] { Spotify, Apple, YouTube } });

        status.ShouldBe(HttpStatusCode.OK);
        Delivery(body, "Spotify").GetProperty("status").GetString().ShouldBe("live");
        Delivery(body, "Apple Music").GetProperty("submittedAtUtc").GetDateTime().ShouldBe(firstSent);
        var resent = Delivery(body, "YouTube Music");
        resent.GetProperty("status").GetString().ShouldBe("pending");
        resent.GetProperty("answeredAtUtc").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Distribute_NothingToSend_Is200_WithTheTrack()
    {
        var id = await NewTrackAsync();
        await DistributeAsync(id, new { dspIds = new[] { Spotify } });

        var (status, body) = await DistributeAsync(id, new { dspIds = new[] { Spotify } });

        status.ShouldBe(HttpStatusCode.OK);
        body.RootElement.GetProperty("distributions").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Distribute_Draft_Is409()
    {
        var id = await NewTrackAsync(submit: false);

        var (status, body) = await DistributeAsync(id, new { dspIds = new[] { Spotify } });

        status.ShouldBe(HttpStatusCode.Conflict);
        body.RootElement.GetProperty("detail").GetString().ShouldBe("A draft track must be submitted before it can be distributed.");
    }

    public static TheoryData<string, string> BadBodies => new()
    {
        { "{}", "Choose at least one platform." },
        { """{"dspIds":[]}""", "Choose at least one platform." },
        { $$"""{"dspIds":[{{string.Join(",", Enumerable.Range(0, 21).Select(_ => $"\"{Guid.NewGuid()}\""))}}]}""", "Send to at most 20 different platforms at once." },
    };

    [Theory]
    [MemberData(nameof(BadBodies))]
    public async Task Distribute_BadList_Is400OnDspIds(string json, string message)
    {
        var id = await NewTrackAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{TracksApi.Path}/{id}/distribute")
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new("Bearer", AdminToken);

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = await TracksApi.JsonAsync(response);
        body.RootElement.GetProperty("errors").GetProperty("dspIds").EnumerateArray().Single().GetString().ShouldBe(message);
    }

    [Fact]
    public async Task Distribute_UnknownPlatform_Is400OnDspIds()
    {
        var id = await NewTrackAsync();
        var unknown = Guid.NewGuid();

        var (status, body) = await DistributeAsync(id, new { dspIds = new[] { Spotify, unknown } });

        status.ShouldBe(HttpStatusCode.BadRequest);
        body.RootElement.GetProperty("errors").GetProperty("dspIds").EnumerateArray().Single().GetString()
            .ShouldBe($"Unknown platform: {unknown}.");
    }

    [Fact]
    public async Task Distribute_UnknownTrack_Is404() =>
        (await DistributeAsync(Guid.NewGuid().ToString(), new { dspIds = new[] { Spotify } })).Status.ShouldBe(HttpStatusCode.NotFound);

    [Fact]
    public async Task Writes_NeedAnAdmin()
    {
        var id = await NewTrackAsync();
        var viewer = TestTokens.Create(roles: []);

        (await TracksApi.DistributeAsync(Client, null, id, new { dspIds = new[] { Spotify } })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await TracksApi.DistributeAsync(Client, viewer, id, new { dspIds = new[] { Spotify } })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await TracksApi.AnswerAsync(Client, null, id, Spotify, new { status = "live" })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await TracksApi.AnswerAsync(Client, viewer, id, Spotify, new { status = "live" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Answer_Pending_RecordsIt_AndTheRulesHold()
    {
        var id = await NewTrackAsync();
        await DistributeAsync(id, new { dspIds = new[] { Spotify, Apple } });

        var (status, body) = await AnswerAsync(id, Spotify, "live");
        status.ShouldBe(HttpStatusCode.OK);
        var live = Delivery(body, "Spotify");
        live.GetProperty("status").GetString().ShouldBe("live");
        live.GetProperty("answeredAtUtc").GetDateTime().ShouldBe(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        live.GetProperty("allowedAnswers").GetArrayLength().ShouldBe(0);
        body.RootElement.GetProperty("status").GetString().ShouldBe("distributed");

        (status, body) = await AnswerAsync(id, Spotify, "rejected");
        status.ShouldBe(HttpStatusCode.Conflict);
        body.RootElement.GetProperty("detail").GetString().ShouldBe("A live delivery is final.");

        (await AnswerAsync(id, Apple, "rejected")).Status.ShouldBe(HttpStatusCode.OK);
        (status, body) = await AnswerAsync(id, Apple, "live");
        status.ShouldBe(HttpStatusCode.Conflict);
        body.RootElement.GetProperty("detail").GetString()
            .ShouldBe("A rejected delivery must be sent again with Distribute before the platform can answer.");
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("Live")]
    [InlineData(null)]
    public async Task Answer_BadStatus_Is400(string? value)
    {
        var id = await NewTrackAsync();
        await DistributeAsync(id, new { dspIds = new[] { Spotify } });

        var (status, body) = await AnswerAsync(id, Spotify, value);

        status.ShouldBe(HttpStatusCode.BadRequest);
        body.RootElement.GetProperty("errors").GetProperty("status").EnumerateArray().Single().GetString()
            .ShouldBe("Status must be live or rejected.");
    }

    [Fact]
    public async Task Answer_NotSentThere_Is404()
    {
        var id = await NewTrackAsync();
        await DistributeAsync(id, new { dspIds = new[] { Spotify } });

        var (status, body) = await AnswerAsync(id, YouTube, "live");

        status.ShouldBe(HttpStatusCode.NotFound);
        body.RootElement.GetProperty("detail").GetString().ShouldBe("This track has not been sent to this platform.");
        (await AnswerAsync(Guid.NewGuid().ToString(), Spotify, "live")).Status.ShouldBe(HttpStatusCode.NotFound);
    }
}
