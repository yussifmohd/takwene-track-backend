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
/// draft → submitted, submitted → draft (withdraw), submitted → distributed only when the track has at
/// least one delivery (in practice <see cref="Track.Distribute"/> makes that move). Distributed is final.
/// </summary>
public static class TrackStatusRules
{
    public const string NeedsDeliveryMessage =
        "A track becomes distributed when it is sent to at least one platform. Use Distribute to send it.";

    private static readonly Dictionary<TrackStatus, TrackStatus[]> Allowed = new()
    {
        [TrackStatus.Draft] = [TrackStatus.Submitted],
        [TrackStatus.Submitted] = [TrackStatus.Draft, TrackStatus.Distributed],
        [TrackStatus.Distributed] = [],
    };

    private static readonly TrackStatus[] SubmittedWithoutDeliveries = [TrackStatus.Draft];

    public static IReadOnlyList<TrackStatus> AllowedNext(TrackStatus from, bool hasDistributions) =>
        from == TrackStatus.Submitted && !hasDistributions ? SubmittedWithoutDeliveries : Allowed[from];

    public static StatusChange Check(TrackStatus from, TrackStatus to, bool hasDistributions)
    {
        if (from == to)
        {
            return new(StatusChangeOutcome.AlreadyInStatus, $"The track is already {from.ToName()}.");
        }

        if (from == TrackStatus.Submitted && to == TrackStatus.Distributed && !hasDistributions)
        {
            return new(StatusChangeOutcome.NotAllowed, NeedsDeliveryMessage);
        }

        if (Allowed[from].Contains(to))
        {
            return new(StatusChangeOutcome.Changed, null);
        }

        return new(StatusChangeOutcome.NotAllowed, from switch
        {
            TrackStatus.Distributed => "A distributed track is final; its status cannot change.",
            TrackStatus.Draft => DraftNotDistributableMessage,
            _ => $"A {from.ToName()} track cannot change to {to.ToName()}.",
        });
    }

    public const string DraftNotDistributableMessage = "A draft track must be submitted before it can be distributed.";
}
