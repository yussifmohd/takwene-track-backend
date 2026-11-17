namespace TrackManagement.Domain.Tracks;

/// <summary>The fixed genre list. Names are matched exactly (case and punctuation included).</summary>
public static class Genres
{
    public const int MaxLength = 20;

    public static readonly IReadOnlyList<string> All =
        ["Arabic Pop", "Mahraganat", "Khaleeji", "Hip-Hop", "Electronic", "Rap", "R&B", "Rock"];

    public static bool IsKnown(string? name) => name is not null && All.Contains(name, StringComparer.Ordinal);
}
