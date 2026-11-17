using TrackManagement.Domain.Tracks;

namespace TrackManagement.UnitTests.Domain.Tracks;

public sealed class TrackStatusRulesTests
{
    public static TheoryData<TrackStatus, TrackStatus, StatusChangeOutcome, string?> Table => new()
    {
        { TrackStatus.Draft, TrackStatus.Submitted, StatusChangeOutcome.Changed, null },
        { TrackStatus.Submitted, TrackStatus.Draft, StatusChangeOutcome.Changed, null },
        { TrackStatus.Submitted, TrackStatus.Distributed, StatusChangeOutcome.Changed, null },
        { TrackStatus.Draft, TrackStatus.Draft, StatusChangeOutcome.AlreadyInStatus, "The track is already draft." },
        { TrackStatus.Submitted, TrackStatus.Submitted, StatusChangeOutcome.AlreadyInStatus, "The track is already submitted." },
        { TrackStatus.Distributed, TrackStatus.Distributed, StatusChangeOutcome.AlreadyInStatus, "The track is already distributed." },
        { TrackStatus.Draft, TrackStatus.Distributed, StatusChangeOutcome.NotAllowed, "A draft track must be submitted before it can be distributed." },
        { TrackStatus.Distributed, TrackStatus.Draft, StatusChangeOutcome.NotAllowed, "A distributed track is final; its status cannot change." },
        { TrackStatus.Distributed, TrackStatus.Submitted, StatusChangeOutcome.NotAllowed, "A distributed track is final; its status cannot change." },
    };

    [Theory]
    [MemberData(nameof(Table))]
    public void Check_FollowsTheTransitionTable(TrackStatus from, TrackStatus to, StatusChangeOutcome outcome, string? message)
    {
        var result = TrackStatusRules.Check(from, to);

        result.Outcome.ShouldBe(outcome);
        result.Message.ShouldBe(message);
    }

    [Fact]
    public void AllowedNext_ListsOnlyTheAllowedTargets()
    {
        TrackStatusRules.AllowedNext(TrackStatus.Draft).ShouldBe([TrackStatus.Submitted]);
        TrackStatusRules.AllowedNext(TrackStatus.Submitted).ShouldBe([TrackStatus.Draft, TrackStatus.Distributed]);
        TrackStatusRules.AllowedNext(TrackStatus.Distributed).ShouldBeEmpty();
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
