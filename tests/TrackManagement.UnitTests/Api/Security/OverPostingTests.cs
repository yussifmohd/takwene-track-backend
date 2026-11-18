using System.Net;
using TrackManagement.UnitTests.Api.Artists;
using TrackManagement.UnitTests.Api.Auth;
using TrackManagement.UnitTests.Api.Tracks;

namespace TrackManagement.UnitTests.Api.Security;

/// <summary>Bodies bind only to request records: fields the client must not set are ignored.</summary>
public sealed class OverPostingTests
{
    private static readonly Guid ChosenId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public async Task CreateArtist_IgnoresIdAndTimestamps()
    {
        // Arrange
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();
        var token = await AuthApi.LoginAsAdminAsync(client);

        // Act
        var response = await ArtistsApi.CreateAsync(client, token, new
        {
            id = ChosenId,
            name = "Wegz",
            countryCode = "EG",
            email = "wegz@example.com",
            createdAtUtc = "2000-01-01T00:00:00Z",
            updatedAtUtc = "2000-01-01T00:00:00Z",
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var json = await ArtistsApi.JsonAsync(response);
        json.RootElement.GetProperty("id").GetGuid().ShouldNotBe(ChosenId);
        json.RootElement.GetProperty("createdAtUtc").GetDateTime().Year.ShouldNotBe(2000);
    }

    [Fact]
    public async Task CreateTrack_IgnoresIdStatusVersionAndDeliveries()
    {
        // Arrange
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();
        var token = await AuthApi.LoginAsAdminAsync(client);
        var artistId = await TracksApi.CreateArtistAsync(client, token);

        // Act
        var response = await TracksApi.CreateAsync(client, token, new
        {
            id = ChosenId,
            title = "El Bakht",
            artistId,
            isrc = "EG-A1B-26-00001",
            genre = "Rap",
            releaseDate = "2026-12-01",
            status = "distributed",
            version = 42,
            distributions = new[] { new { dspId = Guid.NewGuid(), status = "live" } },
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var json = await TracksApi.JsonAsync(response);
        json.RootElement.GetProperty("id").GetGuid().ShouldNotBe(ChosenId);
        json.RootElement.GetProperty("status").GetString().ShouldBe("draft");
        json.RootElement.GetProperty("distributions").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task ChangeStatus_IgnoresExtraFields()
    {
        // Arrange
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();
        var token = await AuthApi.LoginAsAdminAsync(client);
        var artistId = await TracksApi.CreateArtistAsync(client, token);
        var trackId = await TracksApi.CreateTrackAsync(client, token, artistId, "EG-A1B-26-00002");

        // Act
        var response = await TracksApi.ChangeStatusAsync(client, token, trackId.ToString(), new
        {
            status = "submitted",
            title = "Changed title",
            artistId = Guid.NewGuid(),
            distributions = new[] { new { dspId = Guid.NewGuid(), status = "live" } },
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = await TracksApi.JsonAsync(response);
        json.RootElement.GetProperty("status").GetString().ShouldBe("submitted");
        json.RootElement.GetProperty("title").GetString().ShouldBe("Track");
        json.RootElement.GetProperty("artistId").GetGuid().ShouldBe(artistId);
        json.RootElement.GetProperty("distributions").GetArrayLength().ShouldBe(0);
    }
}
