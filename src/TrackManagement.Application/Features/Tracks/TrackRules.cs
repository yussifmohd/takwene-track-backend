using System.Globalization;
using FluentValidation;
using TrackManagement.Domain.Tracks;

namespace TrackManagement.Application.Features.Tracks;

/// <summary>Shared field rules and messages for track requests (create and list filters).</summary>
public static class TrackRules
{
    public static readonly string GenreMessage = $"Genre must be one of: {string.Join(", ", Genres.All)}.";
    public static readonly string StatusMessage = $"Status must be one of: {string.Join(", ", TrackStatusNames.All)}.";

    /// <summary>"2026-12-01" only; impossible dates ("2026-02-30") and other layouts are refused.</summary>
    public static bool TryParseReleaseDate(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    public static IRuleBuilderOptions<T, string?> KnownGenre<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(Genres.IsKnown).WithMessage(GenreMessage);

    public static IRuleBuilderOptions<T, string?> KnownStatus<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(s => TrackStatusNames.TryParse(s, out _)).WithMessage(StatusMessage);
}
