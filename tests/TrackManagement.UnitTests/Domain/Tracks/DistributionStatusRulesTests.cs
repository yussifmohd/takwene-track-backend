using TrackManagement.Domain.Tracks;

namespace TrackManagement.UnitTests.Domain.Tracks;

public sealed class DistributionStatusRulesTests
{
    public static TheoryData<DistributionStatus, DistributionStatus, StatusChangeOutcome, string?> Table => new()
    {
        { DistributionStatus.Pending, DistributionStatus.Live, StatusChangeOutcome.Changed, null },
        { DistributionStatus.Pending, DistributionStatus.Rejected, StatusChangeOutcome.Changed, null },
        { DistributionStatus.Live, DistributionStatus.Live, StatusChangeOutcome.AlreadyInStatus, "The delivery is already live." },
        { DistributionStatus.Live, DistributionStatus.Rejected, StatusChangeOutcome.NotAllowed, "A live delivery is final." },
        { DistributionStatus.Rejected, DistributionStatus.Rejected, StatusChangeOutcome.AlreadyInStatus, "The delivery is already rejected." },
        { DistributionStatus.Rejected, DistributionStatus.Live, StatusChangeOutcome.NotAllowed, "A rejected delivery must be sent again with Distribute before the platform can answer." },
        { DistributionStatus.Pending, DistributionStatus.Pending, StatusChangeOutcome.NotAllowed, "A delivery becomes pending only by distributing it." },
        { DistributionStatus.Live, DistributionStatus.Pending, StatusChangeOutcome.NotAllowed, "A delivery becomes pending only by distributing it." },
    };

    [Theory]
    [MemberData(nameof(Table))]
    public void Check_FollowsTheAnswerTable(DistributionStatus from, DistributionStatus to, StatusChangeOutcome outcome, string? message)
    {
        var result = DistributionStatusRules.Check(from, to);

        result.Outcome.ShouldBe(outcome);
        result.Message.ShouldBe(message);
    }

    [Fact]
    public void AllowedAnswers_OnlyWhilePending()
    {
        DistributionStatusRules.AllowedAnswers(DistributionStatus.Pending).ShouldBe([DistributionStatus.Live, DistributionStatus.Rejected]);
        DistributionStatusRules.AllowedAnswers(DistributionStatus.Live).ShouldBeEmpty();
        DistributionStatusRules.AllowedAnswers(DistributionStatus.Rejected).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("pending", DistributionStatus.Pending)]
    [InlineData("live", DistributionStatus.Live)]
    [InlineData("rejected", DistributionStatus.Rejected)]
    public void Names_RoundTrip(string name, DistributionStatus status)
    {
        DistributionStatusNames.TryParse(name, out var parsed).ShouldBeTrue();
        parsed.ShouldBe(status);
        status.ToName().ShouldBe(name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Live")]
    [InlineData("published")]
    public void Names_UnknownValues_AreRefused(string? name) => DistributionStatusNames.TryParse(name, out _).ShouldBeFalse();
}
