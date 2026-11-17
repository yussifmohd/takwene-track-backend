namespace TrackManagement.Domain.Tracks;

public enum StatusChangeOutcome
{
    Changed,
    AlreadyInStatus,
    NotAllowed,
}

/// <summary>Result of asking a track to change status. Not an exception: refusals are normal business answers.</summary>
public readonly record struct StatusChange(StatusChangeOutcome Outcome, string? Message)
{
    public bool IsChanged => Outcome == StatusChangeOutcome.Changed;
}

/// <summary>
/// The one place that knows which status changes are allowed:
/// draft → submitted, submitted → draft (withdraw), submitted → distributed. Distributed is final.
/// </summary>
public static class TrackStatusRules
{
    private static readonly Dictionary<TrackStatus, TrackStatus[]> Allowed = new()
    {
        [TrackStatus.Draft] = [TrackStatus.Submitted],
        [TrackStatus.Submitted] = [TrackStatus.Draft, TrackStatus.Distributed],
        [TrackStatus.Distributed] = [],
    };

    public static IReadOnlyList<TrackStatus> AllowedNext(TrackStatus from) => Allowed[from];

    public static StatusChange Check(TrackStatus from, TrackStatus to)
    {
        if (from == to)
        {
            return new(StatusChangeOutcome.AlreadyInStatus, $"The track is already {from.ToName()}.");
        }

        if (Allowed[from].Contains(to))
        {
            return new(StatusChangeOutcome.Changed, null);
        }

        return new(StatusChangeOutcome.NotAllowed, from switch
        {
            TrackStatus.Distributed => "A distributed track is final; its status cannot change.",
            TrackStatus.Draft => "A draft track must be submitted before it can be distributed.",
            _ => $"A {from.ToName()} track cannot change to {to.ToName()}.",
        });
    }
}
