using TrackManagement.Domain.Common;

namespace TrackManagement.Domain.Dsps;

/// <summary>
/// A distribution platform (DSP). The set is fixed and created by the database migration with the
/// same ids in every environment, so tracks can reference them before any admin UI exists.
/// </summary>
public sealed class Dsp : BaseEntity<Guid>
{
    public const int NameMaxLength = 100;

    public static readonly Guid SpotifyId = new("0d1f6b9e-5c2a-4c7e-9f4b-2b8a7c1e0001");
    public static readonly Guid AppleMusicId = new("0d1f6b9e-5c2a-4c7e-9f4b-2b8a7c1e0002");
    public static readonly Guid YouTubeMusicId = new("0d1f6b9e-5c2a-4c7e-9f4b-2b8a7c1e0003");

    private Dsp()
    {
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>The three platforms of the assignment: Spotify, Apple Music, YouTube Music.</summary>
    public static IReadOnlyList<Dsp> Seed { get; } =
    [
        new() { Id = SpotifyId, Name = "Spotify" },
        new() { Id = AppleMusicId, Name = "Apple Music" },
        new() { Id = YouTubeMusicId, Name = "YouTube Music" },
    ];
}
