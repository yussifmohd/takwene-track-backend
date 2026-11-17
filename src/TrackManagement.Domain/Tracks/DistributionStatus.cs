namespace TrackManagement.Domain.Tracks;

/// <summary>State of one delivery to one platform. Wire and database names are the lower-case member names.</summary>
public enum DistributionStatus
{
    Pending,
    Live,
    Rejected,
}

public static class DistributionStatusNames
{
    public static readonly IReadOnlyList<string> All = ["pending", "live", "rejected"];

    /// <summary>The answers a platform can give (what the answer endpoint accepts).</summary>
    public static readonly IReadOnlyList<string> Answers = ["live", "rejected"];

    public static string ToName(this DistributionStatus status) => status switch
    {
        DistributionStatus.Pending => "pending",
        DistributionStatus.Live => "live",
        DistributionStatus.Rejected => "rejected",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static DistributionStatus Parse(string name) =>
        TryParse(name, out var status) ? status : throw new ArgumentException($"Unknown delivery status '{name}'.", nameof(name));

    public static bool TryParse(string? name, out DistributionStatus status)
    {
        switch (name)
        {
            case "pending": status = DistributionStatus.Pending; return true;
            case "live": status = DistributionStatus.Live; return true;
            case "rejected": status = DistributionStatus.Rejected; return true;
            default: status = default; return false;
        }
    }
}

/// <summary>
/// Which platform answers are allowed: pending → live or rejected, nothing else. Pending is reached only
/// by distributing (a new delivery, or a rejected one sent again).
/// </summary>
public static class DistributionStatusRules
{
    private static readonly DistributionStatus[] FromPending = [DistributionStatus.Live, DistributionStatus.Rejected];

    public static IReadOnlyList<DistributionStatus> AllowedAnswers(DistributionStatus from) =>
        from == DistributionStatus.Pending ? FromPending : [];

    public static StatusChange Check(DistributionStatus from, DistributionStatus to)
    {
        if (to == DistributionStatus.Pending)
        {
            return new(StatusChangeOutcome.NotAllowed, "A delivery becomes pending only by distributing it.");
        }

        if (from == to)
        {
            return new(StatusChangeOutcome.AlreadyInStatus, $"The delivery is already {to.ToName()}.");
        }

        return from switch
        {
            DistributionStatus.Pending => new(StatusChangeOutcome.Changed, null),
            DistributionStatus.Live => new(StatusChangeOutcome.NotAllowed, "A live delivery is final."),
            _ => new(StatusChangeOutcome.NotAllowed,
                "A rejected delivery must be sent again with Distribute before the platform can answer."),
        };
    }
}
