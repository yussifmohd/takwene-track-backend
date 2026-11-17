using TrackManagement.Domain.Tracks;

namespace TrackManagement.UnitTests.Domain.Tracks;

public sealed class TrackStatusRulesTests
{
    private const string NeedsDelivery =
        "A track becomes distributed when it is sent to at least one platform. Use Distribute to send it.";

    public static TheoryData<TrackStatus, TrackStatus, bool, StatusChangeOutcome, string?> Table => new()
    {
        { TrackStatus.Draft, TrackStatus.Submitted, false, StatusChangeOutcome.Changed, null },
        { TrackStatus.Submitted, TrackStatus.Draft, false, StatusChangeOutcome.Changed, null },
        { TrackStatus.Submitted, TrackStatus.Distributed, true, StatusChangeOutcome.Changed, null },
        { TrackStatus.Submitted, TrackStatus.Distributed, false, StatusChangeOutcome.NotAllowed, NeedsDelivery },
        { TrackStatus.Draft, TrackStatus.Draft, false, StatusChangeOutcome.AlreadyInStatus, "The track is already draft." },
        { TrackStatus.Submitted, TrackStatus.Submitted, false, StatusChangeOutcome.AlreadyInStatus, "The track is already submitted." },
        { TrackStatus.Distributed, TrackStatus.Distributed, true, StatusChangeOutcome.AlreadyInStatus, "The track is already distributed." },
        { TrackStatus.Draft, TrackStatus.Distributed, false, StatusChangeOutcome.NotAllowed, "A draft track must be submitted before it can be distributed." },
        { TrackStatus.Distributed, TrackStatus.Draft, true, StatusChangeOutcome.NotAllowed, "A distributed track is final; its status cannot change." },
        { TrackStatus.Distributed, TrackStatus.Submitted, true, StatusChangeOutcome.NotAllowed, "A distributed track is final; its status cannot change." },
    };

    [Theory]
    [MemberData(nameof(Table))]
    public void Check_FollowsTheTransitionTable(TrackStatus from, TrackStatus to, bool hasDistributions, StatusChangeOutcome outcome, string? message)
    {
        var result = TrackStatusRules.Check(from, to, hasDistributions);

        result.Outcome.ShouldBe(outcome);
        result.Message.ShouldBe(message);
    }

    [Fact]
    public void AllowedNext_ListsOnlyTheAllowedTargets()
    {
        TrackStatusRules.AllowedNext(TrackStatus.Draft, hasDistributions: false).ShouldBe([TrackStatus.Submitted]);
        TrackStatusRules.AllowedNext(TrackStatus.Submitted, hasDistributions: false).ShouldBe([TrackStatus.Draft]);
        TrackStatusRules.AllowedNext(TrackStatus.Submitted, hasDistributions: true).ShouldBe([TrackStatus.Draft, TrackStatus.Distributed]);
        TrackStatusRules.AllowedNext(TrackStatus.Distributed, hasDistributions: true).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("draft", TrackStatus.Draft)]
    [InlineData("submitted", TrackStatus.Submitted)]
    [InlineData("distributed", TrackStatus.Distributed)]
    public void Names_RoundTrip(string name, TrackStatus status)
    {
        TrackStatusNames.TryParse(name, out var parsed).ShouldBeTrue();
        parsed.ShouldBe(status);
        status.ToName().ShouldBe(name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Draft")]
    [InlineData("published")]
    [InlineData("0")]
    public void Names_UnknownValues_AreRefused(string? name) => TrackStatusNames.TryParse(name, out _).ShouldBeFalse();
}
