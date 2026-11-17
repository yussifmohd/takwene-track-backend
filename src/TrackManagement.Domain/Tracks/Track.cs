using TrackManagement.Domain.Artists;
using TrackManagement.Domain.Common;

namespace TrackManagement.Domain.Tracks;

/// <summary>
/// A recording in the catalog. New tracks are drafts; the status only moves through
/// <see cref="ChangeStatus"/>, which applies <see cref="TrackStatusRules"/>. Values are validated before
/// they get here; the guards are a last line of defence.
/// </summary>
public sealed class Track : BaseEntity<Guid>, IAuditableEntity
{
    public const int TitleMaxLength = 200;
    public static readonly DateOnly MinReleaseDate = new(1900, 1, 1);
    public static readonly DateOnly MaxReleaseDate = new(2100, 12, 31);

    private Track()
    {
    }

    public string Title { get; private set; } = string.Empty;

    public Guid ArtistId { get; private set; }

    /// <summary>Navigation for queries (artist name); never set by the domain.</summary>
    public Artist Artist { get; private set; } = null!;

    /// <summary>12 upper-case characters without hyphens (see <see cref="Tracks.Isrc"/>).</summary>
    public string Isrc { get; private set; } = string.Empty;

    public string Genre { get; private set; } = string.Empty;

    public DateOnly ReleaseDate { get; private set; }

    public TrackStatus Status { get; private set; }

    /// <summary>Concurrency token (PostgreSQL xmin): two changes to the same loaded version cannot both be saved.</summary>
    public uint Version { get; private set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public static Track Create(string title, Guid artistId, string isrc, string genre, DateOnly releaseDate, DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (artistId == Guid.Empty)
        {
            throw new ArgumentException("An artist is required.", nameof(artistId));
        }

        if (!Tracks.Isrc.TryNormalize(isrc, out var normalizedIsrc))
        {
            throw new ArgumentException($"'{isrc}' is not a valid ISRC.", nameof(isrc));
        }

        if (!Genres.IsKnown(genre))
        {
            throw new ArgumentException($"'{genre}' is not a known genre.", nameof(genre));
        }

        if (releaseDate < MinReleaseDate || releaseDate > MaxReleaseDate)
        {
            throw new ArgumentOutOfRangeException(nameof(releaseDate), releaseDate, "Release date must be between 1900 and 2100.");
        }

        if (createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Creation time must be UTC.", nameof(createdAtUtc));
        }

        return new Track
        {
            Id = Guid.CreateVersion7(),
            Title = title.Trim(),
            ArtistId = artistId,
            Isrc = normalizedIsrc,
            Genre = genre,
            ReleaseDate = releaseDate,
            Status = TrackStatus.Draft,
            CreatedAtUtc = createdAtUtc,
        };
    }

    /// <summary>Moves the track to <paramref name="target"/> if the rules allow it; otherwise nothing changes.</summary>
    public StatusChange ChangeStatus(TrackStatus target, DateTime changedAtUtc)
    {
        var check = TrackStatusRules.Check(Status, target);
        if (check.IsChanged)
        {
            Status = target;
            UpdatedAtUtc = changedAtUtc;
        }

        return check;
    }
}
