using TrackManagement.Domain.Dsps;
using TrackManagement.Domain.Tracks;
using TrackManagement.Infrastructure.Persistence.Seeding;

namespace TrackManagement.UnitTests.Infrastructure.Seeding;

/// <summary>The sample covers every state the product has and respects every rule, because it is built through the domain.</summary>
public sealed class SampleCatalogTests
{
    private static readonly string[] Mena = ["EG", "LB", "AE", "SA", "PS"];
    private readonly SampleCatalog.Built _sample = SampleCatalog.Build();

    [Fact]
    public void Artists_IncludeMenaAndInternationalArtists()
    {
        _sample.Artists.Count.ShouldBe(17);
        _sample.Artists.Count(a => Mena.Contains(a.CountryCode)).ShouldBeGreaterThanOrEqualTo(8);
        _sample.Artists.Count(a => a.CountryCode is "GB" or "US").ShouldBeGreaterThanOrEqualTo(6);
        _sample.Artists.Select(a => a.CountryCode).ShouldContain("EG");
        _sample.Artists.Select(a => a.CountryCode).ShouldContain("AE");
        _sample.Artists.Select(a => a.CountryCode).ShouldContain("SA");
        _sample.Artists.Select(a => a.CountryCode).ShouldContain("LB");
        _sample.Artists.ShouldContain(a => a.Name.Any(c => c >= '؀' && c <= 'ۿ'), "some names are in Arabic script");
    }

    [Fact]
    public void Artists_HaveUniqueSampleEmails()
    {
        _sample.Artists.ShouldAllBe(a => a.Email.EndsWith("@" + SampleCatalog.EmailDomain, StringComparison.Ordinal));
        _sample.Artists.Select(a => a.NormalizedEmail).Distinct().Count().ShouldBe(_sample.Artists.Count);
    }

    [Fact]
    public void Tracks_CoverEveryGenreAndStatus()
    {
        _sample.Tracks.Count.ShouldBeGreaterThanOrEqualTo(30);
        _sample.Tracks.Select(t => t.Genre).Distinct().OrderBy(g => g).ShouldBe(Genres.All.OrderBy(g => g));
        _sample.Tracks.Select(t => t.Status).Distinct().OrderBy(s => s)
            .ShouldBe([TrackStatus.Draft, TrackStatus.Submitted, TrackStatus.Distributed]);
    }

    [Fact]
    public void Tracks_HaveValidUniqueSampleIsrcs()
    {
        _sample.Tracks.ShouldAllBe(t => Isrc.IsNormalized(t.Isrc));
        _sample.Tracks.ShouldAllBe(t => t.Isrc.StartsWith(SampleCatalog.IsrcPrefix, StringComparison.Ordinal));
        _sample.Tracks.Select(t => t.Isrc).Distinct().Count().ShouldBe(_sample.Tracks.Count);
    }

    [Fact]
    public void Tracks_BelongToSampleArtists()
    {
        var ids = _sample.Artists.Select(a => a.Id).ToHashSet();
        _sample.Tracks.ShouldAllBe(t => ids.Contains(t.ArtistId));
    }

    [Fact]
    public void Deliveries_OnlyOnDistributedTracks_AndEveryDistributedTrackHasOne()
    {
        _sample.Tracks.Where(t => t.Status != TrackStatus.Distributed).ShouldAllBe(t => t.Distributions.Count == 0);
        _sample.Tracks.Where(t => t.Status == TrackStatus.Distributed).ShouldAllBe(t => t.Distributions.Count > 0);
    }

    [Theory]
    [InlineData(DistributionStatus.Pending)]
    [InlineData(DistributionStatus.Live)]
    [InlineData(DistributionStatus.Rejected)]
    public void EveryDeliveryState_OccursOnEveryPlatform(DistributionStatus state)
    {
        var deliveries = _sample.Tracks.SelectMany(t => t.Distributions).ToList();
        foreach (var dspId in new[] { Dsp.SpotifyId, Dsp.AppleMusicId, Dsp.YouTubeMusicId })
        {
            deliveries.ShouldContain(d => d.DspId == dspId && d.Status == state, $"{state} on {dspId}");
        }
    }

    [Fact]
    public void Answers_FollowSends_AndPendingHasNoAnswer()
    {
        var deliveries = _sample.Tracks.SelectMany(t => t.Distributions).ToList();
        deliveries.Where(d => d.Status == DistributionStatus.Pending).ShouldAllBe(d => d.AnsweredAtUtc == null);
        deliveries.Where(d => d.Status != DistributionStatus.Pending)
            .ShouldAllBe(d => d.AnsweredAtUtc != null && d.AnsweredAtUtc > d.SubmittedAtUtc);
    }

    [Fact]
    public void ARejectedDeliverySentAgain_IsPendingWithALaterSendTime()
    {
        var stairway = _sample.Tracks.Single(t => t.Title == "Stairway to Heaven");
        var apple = stairway.Distributions.Single(d => d.DspId == Dsp.AppleMusicId);
        var spotify = stairway.Distributions.Single(d => d.DspId == Dsp.SpotifyId);

        apple.Status.ShouldBe(DistributionStatus.Pending);
        apple.SubmittedAtUtc.ShouldBeGreaterThan(spotify.SubmittedAtUtc);
    }

    [Fact]
    public void Build_IsDeterministic_ExceptForIds()
    {
        var again = SampleCatalog.Build();

        again.Tracks.Select(t => (t.Title, t.Isrc, t.Status, t.ReleaseDate, t.CreatedAtUtc))
            .ShouldBe(_sample.Tracks.Select(t => (t.Title, t.Isrc, t.Status, t.ReleaseDate, t.CreatedAtUtc)));
        again.Artists.Select(a => a.Id).ShouldNotBe(_sample.Artists.Select(a => a.Id));
    }

    [Fact]
    public void Seeds_AreConsistent()
    {
        SampleCatalog.Tracks.Select(t => t.Number).ShouldBe(Enumerable.Range(1, SampleCatalog.Tracks.Count));
        SampleCatalog.Tracks.ShouldAllBe(t => SampleCatalog.Artists.Any(a => a.Key == t.ArtistKey));
        SampleCatalog.Artists.Select(a => a.Key).Distinct().Count().ShouldBe(SampleCatalog.Artists.Count);
        SampleCatalog.Artists.ShouldAllBe(a => SampleCatalog.Tracks.Any(t => t.ArtistKey == a.Key), "every sample artist has a track");
    }
}
