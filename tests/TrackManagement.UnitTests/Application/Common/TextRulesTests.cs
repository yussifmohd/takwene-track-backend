using TrackManagement.Application.Common.Validation;

namespace TrackManagement.UnitTests.Application.Common;

public sealed class TextRulesTests
{
    [Theory]
    [InlineData("abc", 3)]
    [InlineData("عمرو", 4)]
    [InlineData("🎤🎤", 2)] // 4 UTF-16 units, 2 code points
    [InlineData("é", 2)] // combining accent is its own code point
    [InlineData(null, 0)]
    public void CodePointLength_CountsCodePoints(string? value, int expected) => TextRules.CodePointLength(value).ShouldBe(expected);

    [Theory]
    [InlineData("booking@example.com", true)]
    [InlineData("a.b@c.d.eg", true)]
    [InlineData("no-at.example.com", false)]
    [InlineData("two@@example.com", false)]
    [InlineData("a@nodot", false)]
    [InlineData("a@b.", false)]
    [InlineData("a b@c.com", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsEmail(string? value, bool expected) => TextRules.IsEmail(value).ShouldBe(expected);
}
