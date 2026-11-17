using System.Reflection;
using Ardalis.Specification;
using TrackManagement.Application.Common.Persistence;
using TrackManagement.Domain.Dsps;
using TrackManagement.Domain.Artists;
using TrackManagement.Domain.Tracks;

namespace TrackManagement.UnitTests.Application.Tracks;

/// <summary>In-memory tracks with their artist navigation set (as EF would), for evaluating specifications.</summary>
internal static class TrackTestData
{
    public static readonly DateTime Now = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);

    private static readonly PropertyInfo ArtistProperty = typeof(TrackManagement.Domain.Tracks.Track).GetProperty(nameof(TrackManagement.Domain.Tracks.Track.Artist))!;

    /// <summary>A read repository of platforms answering with <see cref="Dsp.Seed"/> for any specification.</summary>
    public static IReadRepository<Dsp> Dsps()
    {
        var dsps = Substitute.For<IReadRepository<Dsp>>();
        dsps.ListAsync(Arg.Any<ISpecification<Dsp>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((ISpecification<Dsp>)ci[0]).Evaluate(Dsp.Seed).ToList());
        return dsps;
    }

    public static Artist Artist(string name, string email) => TrackManagement.Domain.Artists.Artist.Create(name, "EG", email, Now);

    public static Track Track(
        Artist artist, string title, string isrc, string genre = "Rap", string releaseDate = "2026-12-01",
        TrackStatus status = TrackStatus.Draft)
    {
        var track = TrackManagement.Domain.Tracks.Track.Create(title, artist.Id, isrc, genre, DateOnly.Parse(releaseDate, System.Globalization.CultureInfo.InvariantCulture), Now);
        ArtistProperty.SetValue(track, artist);
        if (status != TrackStatus.Draft)
        {
            track.ChangeStatus(TrackStatus.Submitted, Now);
        }

        if (status == TrackStatus.Distributed)
        {
            // Distributed is reached only by sending the track to a platform.
            track.Distribute([TrackManagement.Domain.Dsps.Dsp.SpotifyId], Now);
        }

        return track;
    }
}
