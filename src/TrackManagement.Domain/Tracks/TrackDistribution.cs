using TrackManagement.Domain.Dsps;

namespace TrackManagement.Domain.Tracks;

/// <summary>
/// A track's delivery to one platform: part of the <see cref="Track"/> aggregate (key track + platform),
/// changed only through <see cref="Track.Distribute"/> and <see cref="Track.RecordAnswer"/>.
/// <see cref="AnsweredAtUtc"/> is null exactly while the delivery is pending.
/// </summary>
public sealed class TrackDistribution
{
    private TrackDistribution()
    {
    }

    public Guid TrackId { get; private set; }

    public Guid DspId { get; private set; }

    /// <summary>Navigation for queries (platform name); never set by the domain.</summary>
    public Dsp Dsp { get; private set; } = null!;

    public DistributionStatus Status { get; private set; }

    public DateTime SubmittedAtUtc { get; private set; }

    public DateTime? AnsweredAtUtc { get; private set; }

    internal static TrackDistribution Send(Guid trackId, Guid dspId, DateTime submittedAtUtc) => new()
    {
        TrackId = trackId,
        DspId = dspId,
        Status = DistributionStatus.Pending,
        SubmittedAtUtc = submittedAtUtc,
    };

    /// <summary>True when distributing to this platform would do something (new or rejected).</summary>
    internal bool CanSendAgain => Status == DistributionStatus.Rejected;

    internal void SendAgain(DateTime submittedAtUtc)
    {
        Status = DistributionStatus.Pending;
        SubmittedAtUtc = submittedAtUtc;
        AnsweredAtUtc = null;
    }

    internal StatusChange Answer(DistributionStatus answer, DateTime answeredAtUtc)
    {
        var check = DistributionStatusRules.Check(Status, answer);
        if (check.IsChanged)
        {
            Status = answer;
            AnsweredAtUtc = answeredAtUtc;
        }

        return check;
    }
}

public enum DistributeOutcome
{
    Changed,
    Unchanged,
    NotAllowed,
}

/// <param name="SentDspIds">Platforms that got a new or re-sent delivery, in request order.</param>
public sealed record DistributeResult(DistributeOutcome Outcome, string? Message, IReadOnlyList<Guid> SentDspIds);
