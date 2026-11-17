namespace TrackManagement.Domain.Tracks;

/// <summary>Workflow state of a track. Wire and database names are the lower-case member names.</summary>
public enum TrackStatus
{
    Draft,
    Submitted,
    Distributed,
}

public static class TrackStatusNames
{
    public static readonly IReadOnlyList<string> All = ["draft", "submitted", "distributed"];

    public static string ToName(this TrackStatus status) => status switch
    {
        TrackStatus.Draft => "draft",
        TrackStatus.Submitted => "submitted",
        TrackStatus.Distributed => "distributed",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    /// <summary>Name → status for stored values (always valid); throws on anything else.</summary>
    public static TrackStatus Parse(string name) =>
        TryParse(name, out var status) ? status : throw new ArgumentException($"Unknown track status '{name}'.", nameof(name));

    /// <summary>Exact lower-case names only ("draft"); anything else is unknown.</summary>
    public static bool TryParse(string? name, out TrackStatus status)
    {
        switch (name)
        {
            case "draft": status = TrackStatus.Draft; return true;
            case "submitted": status = TrackStatus.Submitted; return true;
            case "distributed": status = TrackStatus.Distributed; return true;
            default: status = default; return false;
        }
    }
}
