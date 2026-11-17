using TrackManagement.Domain.Artists;
using TrackManagement.Domain.Dsps;

namespace TrackManagement.UnitTests.Domain;

public sealed class ArtistTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_TrimsNameAndEmail_UpperCasesCountry_NormalizesEmail()
    {
        // Act
        var artist = Artist.Create("  عمرو دياب ", " eg ", " Booking@Example.com ", Now);

        // Assert
        artist.Name.ShouldBe("عمرو دياب");
        artist.CountryCode.ShouldBe("EG");
        artist.Email.ShouldBe("Booking@Example.com");
        artist.NormalizedEmail.ShouldBe("BOOKING@EXAMPLE.COM");
        artist.CreatedAtUtc.ShouldBe(Now);
        artist.UpdatedAtUtc.ShouldBeNull();
        artist.Id.ShouldNotBe(Guid.Empty);
    }

    [Theory]
    [InlineData("", "EG", "a@b.co")]
    [InlineData("Name", "XX", "a@b.co")]
    [InlineData("Name", "EG", " ")]
    public void Create_InvalidInput_Throws(string name, string country, string email) =>
        Should.Throw<ArgumentException>(() => Artist.Create(name, country, email, Now));

    [Fact]
    public void Create_NonUtcTime_Throws() =>
        Should.Throw<ArgumentException>(() => Artist.Create("Name", "EG", "a@b.co", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local)));

    [Fact]
    public void Dsp_Seed_IsTheThreePlatformsWithFixedIds()
    {
        Dsp.Seed.Select(d => d.Name).ShouldBe(["Spotify", "Apple Music", "YouTube Music"]);
        Dsp.Seed.Select(d => d.Id).ShouldBe([Dsp.SpotifyId, Dsp.AppleMusicId, Dsp.YouTubeMusicId]);
        Dsp.Seed.Select(d => d.Id).Distinct().Count().ShouldBe(3);
        Dsp.SpotifyId.ShouldBe(new Guid("0d1f6b9e-5c2a-4c7e-9f4b-2b8a7c1e0001"));
    }
}
