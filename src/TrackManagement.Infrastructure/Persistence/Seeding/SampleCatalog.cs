using TrackManagement.Domain.Artists;
using TrackManagement.Domain.Dsps;
using TrackManagement.Domain.Tracks;

namespace TrackManagement.Infrastructure.Persistence.Seeding;

/// <summary>
/// The sample catalogue added on the first start against an empty catalogue.
/// <para>
/// <b>True</b> (verified 2026-11-17, sources in specs/006-…/data-model.md): artist names, countries, song
/// titles, artist–song pairings and genres. <b>Invented sample data</b>: every ISRC (prefix
/// <see cref="IsrcPrefix"/>), email (<c>@example.com</c>), release date, status, delivery and timestamp.
/// </para>
/// Everything is built through the domain (factories, <c>ChangeStatus</c>, <c>Distribute</c>,
/// <c>RecordAnswer</c>), so the sample cannot break a product rule.
/// </summary>
public static class SampleCatalog
{
    /// <summary>Invented ISRC prefix: <c>XX</c> is not a country, <c>SMP</c> stands for "sample".</summary>
    public const string IsrcPrefix = "XXSMP26";

    public const string EmailDomain = "example.com";

    /// <summary>Fixed base time, so the sample is identical on every machine.</summary>
    public static readonly DateTime BaseTimeUtc = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    public enum Delivery
    {
        Pending,
        Live,
        Rejected,

        /// <summary>Rejected, then sent again: pending with a later send time.</summary>
        SentAgain,
    }

    public sealed record ArtistSeed(string Key, string Name, string CountryCode);

    public sealed record TrackSeed(
        int Number,
        string ArtistKey,
        string Title,
        string Genre,
        TrackStatus Status,
        IReadOnlyList<(Guid DspId, Delivery State)> Deliveries);

    private static readonly Guid S = Dsp.SpotifyId;
    private static readonly Guid A = Dsp.AppleMusicId;
    private static readonly Guid Y = Dsp.YouTubeMusicId;

    public static readonly IReadOnlyList<ArtistSeed> Artists =
    [
        new("amr-diab", "Amr Diab", "EG"),
        new("wegz", "Wegz", "EG"),
        new("hassan-shakosh", "حسن شاكوش", "EG"),
        new("cairokee", "Cairokee", "EG"),
        new("nancy-ajram", "نانسي عجرم", "LB"),
        new("elissa", "Elissa", "LB"),
        new("hussain-al-jassmi", "حسين الجسمي", "AE"),
        new("mohammed-abdu", "محمد عبده", "SA"),
        new("saint-levant", "Saint Levant", "PS"),
        new("the-beatles", "The Beatles", "GB"),
        new("led-zeppelin", "Led Zeppelin", "GB"),
        new("queen", "Queen", "GB"),
        new("the-prodigy", "The Prodigy", "GB"),
        new("michael-jackson", "Michael Jackson", "US"),
        new("whitney-houston", "Whitney Houston", "US"),
        new("eminem", "Eminem", "US"),
        new("the-notorious-big", "The Notorious B.I.G.", "US"),
    ];

    private const TrackStatus Draft = TrackStatus.Draft;
    private const TrackStatus Submitted = TrackStatus.Submitted;
    private const TrackStatus Distributed = TrackStatus.Distributed;
    private const Delivery P = Delivery.Pending;
    private const Delivery L = Delivery.Live;
    private const Delivery R = Delivery.Rejected;

    public static readonly IReadOnlyList<TrackSeed> Tracks =
    [
        new(1, "amr-diab", "Tamally Maak", "Arabic Pop", Distributed, [(S, L), (A, L), (Y, L)]),
        new(2, "amr-diab", "Nour El Ein", "Arabic Pop", Submitted, []),
        new(3, "amr-diab", "Wayah", "Arabic Pop", Distributed, [(A, P)]),
        new(4, "wegz", "El Bakht", "Rap", Distributed, [(S, L), (Y, P)]),
        new(5, "wegz", "Dorak Gai", "Rap", Draft, []),
        new(6, "hassan-shakosh", "بنت الجيران", "Mahraganat", Distributed, [(Y, L), (S, R)]),
        new(7, "cairokee", "Ya El Midan", "Rock", Submitted, []),
        new(8, "nancy-ajram", "آه ونص", "Arabic Pop", Distributed, [(S, L), (A, L), (Y, R)]),
        new(9, "nancy-ajram", "Ya Tabtab", "Arabic Pop", Draft, []),
        new(10, "elissa", "Aa Bali Habibi", "Arabic Pop", Submitted, []),
        new(11, "elissa", "Asaad Wahda", "Arabic Pop", Draft, []),
        new(12, "hussain-al-jassmi", "Ahebak", "Khaleeji", Distributed, [(S, L), (A, L)]),
        new(13, "mohammed-abdu", "Ab'ad", "Khaleeji", Submitted, []),
        new(14, "mohammed-abdu", "الأماكن", "Khaleeji", Distributed, [(Y, R), (S, P)]),
        new(15, "saint-levant", "Very Few Friends", "Rap", Distributed, [(S, L)]),
        new(16, "mohammed-abdu", "Al Rasayel", "Khaleeji", Draft, []),
        new(17, "the-beatles", "Come Together", "Rock", Distributed, [(S, L), (A, L), (Y, L)]),
        new(18, "the-beatles", "Revolution", "Rock", Submitted, []),
        new(19, "the-beatles", "Helter Skelter", "Rock", Draft, []),
        new(20, "led-zeppelin", "Stairway to Heaven", "Rock", Distributed, [(S, L), (A, Delivery.SentAgain)]),
        new(21, "led-zeppelin", "Whole Lotta Love", "Rock", Submitted, []),
        new(22, "michael-jackson", "Billie Jean", "R&B", Distributed, [(S, L), (A, L), (Y, P)]),
        new(23, "michael-jackson", "Remember the Time", "R&B", Draft, []),
        new(24, "queen", "Bohemian Rhapsody", "Rock", Distributed, [(Y, R)]),
        new(25, "led-zeppelin", "Black Dog", "Rock", Submitted, []),
        new(26, "whitney-houston", "I Will Always Love You", "R&B", Distributed, [(A, R), (S, L)]),
        new(27, "eminem", "Lose Yourself", "Rap", Distributed, [(S, P), (A, P), (Y, P)]),
        new(28, "eminem", "Without Me", "Rap", Draft, []),
        new(29, "the-prodigy", "Firestarter", "Electronic", Distributed, [(S, P), (Y, L)]),
        new(30, "the-prodigy", "Breathe", "Electronic", Submitted, []),
        new(31, "the-notorious-big", "Juicy", "Hip-Hop", Submitted, []),
        new(32, "the-notorious-big", "Big Poppa", "Hip-Hop", Draft, []),
    ];

    public sealed record Built(IReadOnlyList<Artist> Artists, IReadOnlyList<Track> Tracks);

    public static string EmailFor(ArtistSeed artist) => $"{artist.Key}@{EmailDomain}";

    public static string IsrcFor(TrackSeed track) => $"{IsrcPrefix}{90000 + track.Number:D5}";

    /// <summary>Builds new domain objects (fresh ids) for the whole sample. Throws if any step is refused.</summary>
    public static Built Build()
    {
        var artists = Artists
            .Select((a, i) => Artist.Create(a.Name, a.CountryCode, EmailFor(a), BaseTimeUtc.AddMinutes(i)))
            .ToList();
        var artistIds = Artists.Zip(artists).ToDictionary(p => p.First.Key, p => p.Second.Id, StringComparer.Ordinal);

        var tracks = Tracks.Select(seed => BuildTrack(seed, artistIds[seed.ArtistKey])).ToList();
        return new Built(artists, tracks);
    }

    private static Track BuildTrack(TrackSeed seed, Guid artistId)
    {
        // Each track has its own day; steps inside it are hours apart, so answers always follow sends.
        var day = BaseTimeUtc.AddDays(seed.Number);
        var releaseDate = new DateOnly(2026, 1 + ((seed.Number - 1) % 12), 1 + ((seed.Number * 7) % 28));
        var track = Track.Create(seed.Title, artistId, IsrcFor(seed), seed.Genre, releaseDate, day);

        if (seed.Status == TrackStatus.Draft)
        {
            return track;
        }

        Expect(track.ChangeStatus(TrackStatus.Submitted, day.AddHours(1)).Outcome == StatusChangeOutcome.Changed, seed, "submit");
        if (seed.Status == TrackStatus.Submitted)
        {
            return track;
        }

        var sentAt = day.AddHours(2);
        var sent = track.Distribute(seed.Deliveries.Select(d => d.DspId).ToList(), sentAt);
        Expect(sent.Outcome == DistributeOutcome.Changed, seed, "distribute");

        var answeredAt = day.AddHours(26);
        foreach (var (dspId, state) in seed.Deliveries)
        {
            switch (state)
            {
                case Delivery.Live:
                    Answer(track, seed, dspId, DistributionStatus.Live, answeredAt);
                    break;
                case Delivery.Rejected:
                    Answer(track, seed, dspId, DistributionStatus.Rejected, answeredAt);
                    break;
                case Delivery.SentAgain:
                    Answer(track, seed, dspId, DistributionStatus.Rejected, answeredAt);
                    Expect(track.Distribute([dspId], answeredAt.AddHours(24)).Outcome == DistributeOutcome.Changed, seed, "send again");
                    break;
                case Delivery.Pending:
                    break;
            }
        }

        return track;
    }

    private static void Answer(Track track, TrackSeed seed, Guid dspId, DistributionStatus answer, DateTime at) =>
        Expect(track.RecordAnswer(dspId, answer, at)?.Outcome == StatusChangeOutcome.Changed, seed, $"answer {answer}");

    private static void Expect(bool ok, TrackSeed seed, string step)
    {
        if (!ok)
        {
            throw new InvalidOperationException($"Sample track {seed.Number} ('{seed.Title}'): the domain refused '{step}'.");
        }
    }
}
