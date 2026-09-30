using System.Text.Json.Serialization;

namespace Kentico.Xperience.AdminStats.Shared;

/// <summary>
/// What a value measures, so the admin client formats it (see <c>formatValue</c> in <c>format.ts</c>).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<StatsValueKind>))]
public enum StatsValueKind
{
    /// <summary>Number of things (orders, items). Can be fractional (for example item quantities).</summary>
    Count,

    /// <summary>Money amount as stored (no currency). Rounded to <see cref="StatsValues.AmountDecimals"/> decimals.</summary>
    Amount,

    /// <summary>
    /// Ratio (0.25 = 25%), for example a rate or share. Rounded to <see cref="StatsValues.RatioDecimals"/> decimals.
    /// Comparisons of ratios change in percentage points, see <see cref="StatsValueComparison.Change"/>.
    /// </summary>
    Ratio,
}

/// <summary>
/// Rounding rules of decimal report values.
/// </summary>
public static class StatsValues
{
    /// <summary>
    /// Decimals of <see cref="StatsValueKind.Amount"/> values.
    /// </summary>
    public const int AmountDecimals = 2;

    /// <summary>
    /// Decimals of <see cref="StatsValueKind.Ratio"/> values (0.1234 = 12.34%).
    /// </summary>
    public const int RatioDecimals = 4;

    /// <summary>
    /// Rounds amounts to <see cref="AmountDecimals"/> decimals (midpoint away from zero, like prices)
    /// and ratios to <see cref="RatioDecimals"/> decimals. Counts are returned as they are.
    /// </summary>
    public static decimal Round(decimal value, StatsValueKind kind) =>
        kind switch
        {
            StatsValueKind.Amount => Math.Round(value, AmountDecimals, MidpointRounding.AwayFromZero),
            StatsValueKind.Ratio => Math.Round(value, RatioDecimals, MidpointRounding.AwayFromZero),
            StatsValueKind.Count => value,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    /// <inheritdoc cref="Round(decimal, StatsValueKind)"/>
    public static decimal? Round(decimal? value, StatsValueKind kind) =>
        value is decimal v ? Round(v, kind) : null;

    /// <summary>
    /// Returns <paramref name="numerator"/> / <paramref name="denominator"/>, or <c>null</c> when <paramref name="denominator"/> is 0
    /// (for example the average order value without orders).
    /// </summary>
    public static decimal? Divide(decimal numerator, decimal denominator) =>
        denominator == 0 ? null : numerator / denominator;
}

/// <summary>
/// A decimal value (count, amount or ratio) in the selected range compared with the previous period.
/// The decimal counterpart of <see cref="StatsComparison"/>, which stays <c>int</c> for count reports.
/// </summary>
/// <param name="Kind">What the value measures.</param>
/// <param name="Current">Value in the selected range (rounded by <paramref name="Kind"/>). <c>null</c> when it cannot be computed (for example an average of nothing).</param>
/// <param name="Previous">Value in the previous period. <c>null</c> when it cannot be computed.</param>
/// <param name="Change">
/// Relative change as a ratio (0.12 = +12%) of the rounded values. <c>null</c> when a value is <c>null</c> or <paramref name="Previous"/> is 0.
/// For <see cref="StatsValueKind.Ratio"/> values it is the difference in ratio points instead (0.05 = +5 percentage points):
/// a relative change of a rate reads wrongly (20% to 30% would be "+50%") and cannot be computed when the previous rate is 0.
/// Then it is <c>null</c> only when a value is <c>null</c>.
/// </param>
/// <param name="PreviousFrom">First day of the previous period (inclusive).</param>
/// <param name="PreviousTo">Last day of the previous period (inclusive).</param>
public sealed record StatsValueComparison(
    StatsValueKind Kind,
    decimal? Current,
    decimal? Previous,
    double? Change,
    DateOnly PreviousFrom,
    DateOnly PreviousTo)
{
    /// <summary>
    /// <see cref="Current"/> formatted by the project's price formatter (amounts only, see <see cref="StatsAmountTexts"/>).
    /// <c>null</c> (left out of the JSON) when not formatted; the client then formats the number.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CurrentText { get; init; }

    /// <summary>
    /// <see cref="Previous"/> formatted like <see cref="CurrentText"/>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PreviousText { get; init; }

    /// <summary>
    /// Creates the comparison for the query's range and its previous period (see <see cref="StatsComparison.GetPreviousRange"/>).
    /// Values are rounded by <paramref name="kind"/> first, so the change matches the shown values.
    /// </summary>
    public static StatsValueComparison Create(StatsQuery query, StatsValueKind kind, decimal? current, decimal? previous)
    {
        var (from, to) = StatsComparison.GetPreviousRange(query);
        decimal? roundedCurrent = StatsValues.Round(current, kind);
        decimal? roundedPrevious = StatsValues.Round(previous, kind);

        double? change = (roundedCurrent, roundedPrevious) switch
        {
            (decimal c, decimal p) when kind == StatsValueKind.Ratio => (double)(c - p),
            (decimal c, decimal p) => StatsComparison.GetChange(c, p),
            _ => null,
        };

        return new(kind, roundedCurrent, roundedPrevious, change, from, to);
    }
}

/// <summary>
/// Value for one series on one day, as returned by a daily SQL aggregate. The decimal counterpart of <see cref="StatsDailyCount"/>.
/// </summary>
public sealed record StatsDailyValue(string SeriesKey, DateOnly Date, decimal Value);

/// <summary>
/// One decimal series aligned with a period axis (<see cref="StatsPeriods.Build"/>).
/// Used when series of one chart measure different things (for example orders and revenue).
/// </summary>
/// <param name="Key">Series key.</param>
/// <param name="DisplayName">Series display name.</param>
/// <param name="Kind">What the values measure.</param>
/// <param name="Values">Value per period (zero-filled, rounded by <paramref name="Kind"/>).</param>
/// <param name="Total">Sum of the unrounded daily values, rounded by <paramref name="Kind"/>.</param>
public sealed record StatsValueSeries(string Key, string DisplayName, StatsValueKind Kind, IReadOnlyList<decimal> Values, decimal Total)
{
    /// <summary>
    /// <see cref="Values"/> formatted by the project's price formatter (amounts only, see <see cref="StatsAmountTexts"/>), aligned with <see cref="Values"/>.
    /// <c>null</c> (left out of the JSON) when not formatted; the client then formats the numbers.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Texts { get; init; }

    /// <summary>
    /// <see cref="Total"/> formatted like <see cref="Texts"/>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TotalText { get; init; }
}
