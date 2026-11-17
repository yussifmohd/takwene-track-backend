namespace TrackManagement.Domain.Tracks;

/// <summary>
/// International Standard Recording Code (ISO 3901): CC-XXX-YY-NNNNN — country/prefix (2 letters),
/// registrant (3 letters or digits), year (2 digits), designation (5 digits). Stored as 12 upper-case
/// characters without hyphens, shown with hyphens.
/// </summary>
public static class Isrc
{
    public const int Length = 12;
    public const string FormatHint = "CC-XXX-YY-NNNNN, e.g. EG-A1B-26-00001";

    /// <summary>
    /// Accepts the code in any letter case with outer spaces, written either with all three hyphens in
    /// the standard positions or with none. Anything else (other separators, partial hyphens) is refused.
    /// </summary>
    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = string.Empty;
        if (input is null)
        {
            return false;
        }

        var value = input.Trim().ToUpperInvariant();
        if (value.Length == Length + 3)
        {
            if (value[2] != '-' || value[6] != '-' || value[9] != '-')
            {
                return false;
            }

            value = string.Concat(value.AsSpan(0, 2), value.AsSpan(3, 3), value.AsSpan(7, 2), value.AsSpan(10, 5));
        }

        if (!IsNormalized(value))
        {
            return false;
        }

        normalized = value;
        return true;
    }

    /// <summary>True for exactly 12 characters: 2 letters A–Z, 3 letters/digits, 7 digits.</summary>
    public static bool IsNormalized(string? value)
    {
        if (value is null || value.Length != Length)
        {
            return false;
        }

        for (var i = 0; i < Length; i++)
        {
            var c = value[i];
            var ok = i switch
            {
                < 2 => c is >= 'A' and <= 'Z',
                < 5 => c is >= 'A' and <= 'Z' or >= '0' and <= '9',
                _ => c is >= '0' and <= '9',
            };
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>"EGA1B2600001" → "EG-A1B-26-00001".</summary>
    public static string Format(string normalized) =>
        normalized.Length == Length
            ? $"{normalized[..2]}-{normalized[2..5]}-{normalized[5..7]}-{normalized[7..]}"
            : normalized;
}
