using TrackManagement.Domain.Tracks;

namespace TrackManagement.UnitTests.Domain.Tracks;

public sealed class IsrcTests
{
    [Theory]
    [InlineData("EG-A1B-26-00001", "EGA1B2600001")]
    [InlineData("eg-a1b-26-00001", "EGA1B2600001")]
    [InlineData("EGA1B2600001", "EGA1B2600001")]
    [InlineData("  ega1b2600001 ", "EGA1B2600001")]
    [InlineData("QZ-ABC-99-12345", "QZABC9912345")]
    [InlineData("US-123-00-00000", "US1230000000")]
    public void TryNormalize_ValidForms_Returns12UpperCharacters(string input, string expected)
    {
        Isrc.TryNormalize(input, out var normalized).ShouldBeTrue();
        normalized.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("EG-A1B-26-0001")]   // designation 4 digits
    [InlineData("EGA1B260001")]      // 11 characters
    [InlineData("EGA1B26000011")]    // 13 characters
    [InlineData("E1-A1B-26-00001")]  // digit in the prefix
    [InlineData("EG-A1B-2X-00001")]  // letter in the year
    [InlineData("EG-A1B-26-0000X")]  // letter in the designation
    [InlineData("EG_A1B_26_00001")]  // wrong separator
    [InlineData("EG.A1B.26.00001")]
    [InlineData("EG-A1B2600001")]    // partial hyphens
    [InlineData("EGA-1B-26-00001")]  // hyphen in the wrong place
    [InlineData("EG A1B 26 00001")]  // inner spaces
    [InlineData("EG-A-B-26-00001")]
    [InlineData("ÉG-A1B-26-00001")]  // non-ASCII letter
    public void TryNormalize_InvalidForms_ReturnsFalse(string? input) =>
        Isrc.TryNormalize(input, out _).ShouldBeFalse();

    [Fact]
    public void Format_AddsHyphensInTheStandardPositions() =>
        Isrc.Format("EGA1B2600001").ShouldBe("EG-A1B-26-00001");
}
