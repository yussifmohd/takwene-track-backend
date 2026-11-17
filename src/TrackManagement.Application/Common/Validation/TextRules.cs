using FluentValidation;

namespace TrackManagement.Application.Common.Validation;

/// <summary>Shared text rules. Lengths count Unicode code points, as PostgreSQL varchar(n) does (an emoji is 1, not 2).</summary>
public static class TextRules
{
    public static int CodePointLength(string? value) => value is null ? 0 : value.EnumerateRunes().Count();

    /// <summary>One "@" with text on both sides, a dot in the domain, no spaces. Full RFC parsing is not useful here.</summary>
    public static bool IsEmail(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var at = value.IndexOf('@', StringComparison.Ordinal);
        return at > 0
            && at == value.LastIndexOf('@')
            && at < value.Length - 1
            && value.IndexOf('.', at) > at + 1
            && !value.EndsWith('.')
            && !value.Any(char.IsWhiteSpace);
    }

    /// <summary>The trimmed value has at most <paramref name="max"/> code points.</summary>
    public static IRuleBuilderOptions<T, string> MaximumCodePoints<T>(this IRuleBuilder<T, string> rule, int max) =>
        rule.Must(value => CodePointLength(value?.Trim()) <= max);
}
