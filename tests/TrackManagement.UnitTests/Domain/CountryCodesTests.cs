using System.Security.Cryptography;
using System.Text;
using TrackManagement.Domain.Countries;

namespace TrackManagement.UnitTests.Domain;

public sealed class CountryCodesTests
{
    [Fact]
    public void Sorted_Has249UniqueSortedCodes_MatchingTheSharedHash()
    {
        // The web app holds the same list; both repos assert this hash so the copies cannot drift.
        CountryCodes.Sorted.Count.ShouldBe(249);
        CountryCodes.Sorted.ShouldBe(CountryCodes.Sorted.Order(StringComparer.Ordinal).Distinct());
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(string.Join(',', CountryCodes.Sorted))));
        hash.ShouldBe(CountryCodes.ListHash);
    }

    [Theory]
    [InlineData("EG")]
    [InlineData("SA")]
    [InlineData("AE")]
    [InlineData("PS")]
    [InlineData("SS")]
    [InlineData("eg")]
    [InlineData(" eg ")]
    public void IsAssigned_AssignedCodeInAnyCase_True(string code) => CountryCodes.IsAssigned(code).ShouldBeTrue();

    [Theory]
    [InlineData("UK")]
    [InlineData("EU")]
    [InlineData("XK")]
    [InlineData("ZZ")]
    [InlineData("EGY")]
    [InlineData("")]
    [InlineData(null)]
    public void IsAssigned_ReservedUserAssignedOrMalformed_False(string? code) => CountryCodes.IsAssigned(code).ShouldBeFalse();

    [Fact]
    public void Normalize_TrimsAndUpperCases() => CountryCodes.Normalize(" eg ").ShouldBe("EG");
}
