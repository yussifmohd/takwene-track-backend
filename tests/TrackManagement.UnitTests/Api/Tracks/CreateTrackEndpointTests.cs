using System.Net;
using TrackManagement.UnitTests.TestSupport;

namespace TrackManagement.UnitTests.Api.Tracks;

public sealed class CreateTrackEndpointTests : TracksApiTestBase
{
    [Fact]
    public async Task Create_AsAdmin_Returns201WithLocationAndDraftDetail()
    {
        // Arrange
        var artistId = await TracksApi.CreateArtistAsync(Client, AdminToken, "عمرو دياب");

        // Act
        var response = await TracksApi.CreateAsync(Client, AdminToken,
            new { title = " تملي معاك ", artistId, isrc = "eg-a1b-26-00001", genre = "Arabic Pop", releaseDate = "2026-12-01" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var json = await TracksApi.JsonAsync(response);
        var root = json.RootElement;
        var id = root.GetProperty("id").GetGuid();
        response.Headers.Location!.ToString().ShouldBe($"/api/tracks/{id}");
        root.GetProperty("title").GetString().ShouldBe("تملي معاك");
        root.GetProperty("artistId").GetGuid().ShouldBe(artistId);
        root.GetProperty("artistName").GetString().ShouldBe("عمرو دياب");
        root.GetProperty("isrc").GetString().ShouldBe("EG-A1B-26-00001");
        root.GetProperty("genre").GetString().ShouldBe("Arabic Pop");
        root.GetProperty("status").GetString().ShouldBe("draft");
        root.GetProperty("releaseDate").GetString().ShouldBe("2026-12-01");
        root.GetProperty("allowedNextStatuses").EnumerateArray().Select(s => s.GetString()).ShouldBe(["submitted"]);
        root.GetProperty("distributions").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Create_WithoutHyphens_IsShownWithHyphens()
    {
        var artistId = await TracksApi.CreateArtistAsync(Client, AdminToken);
        var response = await TracksApi.CreateAsync(Client, AdminToken,
            new { title = "T", artistId, isrc = "EGA1B2600002", genre = "R&B", releaseDate = "2099-01-01" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var json = await TracksApi.JsonAsync(response);
        json.RootElement.GetProperty("isrc").GetString().ShouldBe("EG-A1B-26-00002");
    }

    [Fact]
    public async Task Create_WithoutToken_Returns401()
    {
        var response = await TracksApi.CreateAsync(Client, null, new { title = "T" });
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_WithNonAdminToken_Returns403()
    {
        var response = await TracksApi.CreateAsync(Client, TestTokens.Create(roles: []), new { title = "T" });
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_InvalidFields_Returns400NamingEachField()
    {
        // Act
        var response = await TracksApi.CreateAsync(Client, AdminToken,
            new { title = "", artistId = Guid.NewGuid(), isrc = "EG-A1B-26-0001", genre = "Pop", releaseDate = "2026-02-30" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var json = await TracksApi.JsonAsync(response);
        var errors = json.RootElement.GetProperty("errors");
        errors.GetProperty("title")[0].GetString().ShouldBe("Title is required.");
        errors.GetProperty("artistId")[0].GetString().ShouldBe("Choose an existing artist.");
        errors.GetProperty("isrc")[0].GetString().ShouldBe("ISRC must look like CC-XXX-YY-NNNNN, e.g. EG-A1B-26-00001.");
        errors.GetProperty("genre")[0].GetString().ShouldStartWith("Genre must be one of:");
        errors.GetProperty("releaseDate")[0].GetString().ShouldBe("Release date must be a real date written as YYYY-MM-DD.");
    }

    [Fact]
    public async Task Create_EmptyBody_Returns400ForEveryRequiredField()
    {
        var response = await TracksApi.CreateAsync(Client, AdminToken, new { });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var json = await TracksApi.JsonAsync(response);
        var errors = json.RootElement.GetProperty("errors");
        foreach (var field in new[] { "title", "artistId", "isrc", "genre", "releaseDate" })
        {
            errors.TryGetProperty(field, out _).ShouldBeTrue(field);
        }
    }

    [Fact]
    public async Task Create_DuplicateIsrcInAnotherForm_Returns409OnIsrc()
    {
        // Arrange
        var artistId = await TracksApi.CreateArtistAsync(Client, AdminToken);
        await TracksApi.CreateTrackAsync(Client, AdminToken, artistId, "EG-A1B-26-00001");

        // Act
        var response = await TracksApi.CreateAsync(Client, AdminToken,
            new { title = "Other", artistId, isrc = "ega1b2600001", genre = "Rap", releaseDate = "2026-12-01" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        using var json = await TracksApi.JsonAsync(response);
        json.RootElement.GetProperty("errors").GetProperty("isrc")[0].GetString().ShouldBe("Another track already uses this ISRC.");
    }

    [Fact]
    public async Task Create_MalformedArtistId_Returns400()
    {
        var response = await TracksApi.CreateAsync(Client, AdminToken,
            new { title = "T", artistId = "not-a-guid", isrc = "EGA1B2600001", genre = "Rap", releaseDate = "2026-12-01" });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
