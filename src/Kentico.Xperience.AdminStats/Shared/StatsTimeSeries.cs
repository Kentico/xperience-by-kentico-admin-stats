using System.Numerics;

namespace Kentico.Xperience.AdminStats.Shared;

/// <summary>
/// Count for one series on one day, as returned by a daily SQL aggregate.
/// </summary>
/// <param name="SeriesKey">Series key (for example an activity type code name). Compared case-insensitively.</param>
/// <param name="Date">Day of the count.</param>
/// <param name="Count">Count on that day.</param>
public sealed record StatsDailyCount(string SeriesKey, DateOnly Date, int Count);

/// <summary>
/// Key and display name of a series with a fixed position.
/// </summary>
public sealed record StatsSeriesDefinition(string Key, string DisplayName);

/// <summary>
/// One series of a time series report.
/// </summary>
/// <param name="Key">Series key.</param>
/// <param name="DisplayName">Series display name.</param>
/// <param name="Values">Count per period (zero-filled). Aligns with <see cref="StatsTimeSeriesResult.Periods"/>.</param>
/// <param name="Total">Sum of <paramref name="Values"/>.</param>
public sealed record StatsTimeSeries(string Key, string DisplayName, IReadOnlyList<int> Values, int Total);

/// <summary>
/// Counts per series per period for one range.
/// </summary>
/// <param name="From">Applied range start (inclusive).</param>
/// <param name="To">Applied range end (inclusive).</param>
/// <param name="Grouping">Applied grouping.</param>
/// <param name="ChannelId">Applied channel filter. <c>null</c> for reports without a channel.</param>
/// <param name="Periods">Continuous period axis.</param>
/// <param name="Series">Series in display order.</param>
/// <param name="Total">Sum of all series.</param>
public sealed record StatsTimeSeriesResult(
    DateOnly From,
    DateOnly To,
    StatsGrouping Grouping,
    int? ChannelId,
    IReadOnlyList<StatsPeriod> Periods,
    IReadOnlyList<StatsTimeSeries> Series,
    int Total)
{
    /// <summary>
    /// When the data was read from the database. Can be older than the request when served from cache.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Turns daily counts into period-bucketed, zero-filled series.
/// </summary>
public static class StatsTimeSeriesBuilder
{
    /// <summary>
    /// Builds series in a fixed order. Every defined series is returned, also when it has no data.
    /// Rows with keys that are not defined are ignored.
    /// </summary>
    public static StatsTimeSeriesResult BuildFixed(
        StatsQuery query,
        IEnumerable<StatsDailyCount> dailyCounts,
        IReadOnlyList<StatsSeriesDefinition> definitions)
    {
        var (periods, valuesByKey) = Bucket(query, dailyCounts);

        var series = definitions
            .Select(d =>
            {
                int[] values = valuesByKey.TryGetValue(d.Key, out int[]? found) ? found : new int[periods.Count];
                return new StatsTimeSeries(d.Key, d.DisplayName, values, values.Sum());
            })
            .ToList();

        return Create(query, periods, series);
    }

    /// <summary>
    /// Builds one series per key that has data, sorted by total (descending), then display name.
    /// </summary>
    public static StatsTimeSeriesResult BuildByTotal(
        StatsQuery query,
        IEnumerable<StatsDailyCount> dailyCounts,
        Func<string, string> getDisplayName) =>
        BuildDynamic(query, dailyCounts, getDisplayName);

    /// <summary>
    /// Default series that <see cref="BuildDynamic"/> folds the smaller series into.
    /// The key contains characters that code names cannot contain, so it does not collide with data keys.
    /// </summary>
    public static StatsSeriesDefinition OtherSeries { get; } = new("(other)", "Other");

    /// <summary>
    /// Builds one series per key that has data (keys come from the data, not a fixed list),
    /// sorted by total (descending), then display name.
    /// When <paramref name="maxSeries"/> is set and more keys have data, the first <c>maxSeries</c> series are kept
    /// and the rest are summed into one <paramref name="other"/> series, added last.
    /// </summary>
    /// <param name="query">Normalized filter.</param>
    /// <param name="dailyCounts">Daily counts.</param>
    /// <param name="getDisplayName">Returns the display name of a key.</param>
    /// <param name="maxSeries">Optional number of series to keep. Values &lt; 1 are treated as 1. <c>null</c> keeps all.</param>
    /// <param name="other">Key and display name of the folded series. Defaults to <see cref="OtherSeries"/>.</param>
    public static StatsTimeSeriesResult BuildDynamic(
        StatsQuery query,
        IEnumerable<StatsDailyCount> dailyCounts,
        Func<string, string> getDisplayName,
        int? maxSeries = null,
        StatsSeriesDefinition? other = null)
    {
        var (periods, valuesByKey) = Bucket(query, dailyCounts);

        var series = valuesByKey
            .Select(pair => new StatsTimeSeries(pair.Key, getDisplayName(pair.Key), pair.Value, pair.Value.Sum()))
            .OrderByDescending(s => s.Total)
            .ThenBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (maxSeries is int max && series.Count > Math.Max(max, 1))
        {
            int keep = Math.Max(max, 1);
            int[] folded = new int[periods.Count];

            foreach (var small in series.Skip(keep))
            {
                for (int i = 0; i < folded.Length; i++)
                {
                    folded[i] += small.Values[i];
                }
            }

            var otherDefinition = other ?? OtherSeries;
            series = [.. series.Take(keep), new StatsTimeSeries(otherDefinition.Key, otherDefinition.DisplayName, folded, folded.Sum())];
        }

        return Create(query, periods, series);
    }

    /// <summary>
    /// Builds one decimal series on the query's period axis (<see cref="StatsPeriods.Build"/>), zero-filled.
    /// Rows of other keys, outside the range or with a value &lt;= 0 are ignored. Period values and the total are rounded by <paramref name="kind"/>.
    /// </summary>
    /// <param name="query">Normalized filter.</param>
    /// <param name="dailyValues">Daily values.</param>
    /// <param name="definition">Key (compared case-insensitively) and display name of the series.</param>
    /// <param name="kind">What the values measure.</param>
    public static StatsValueSeries BuildValueSeries(
        StatsQuery query,
        IEnumerable<StatsDailyValue> dailyValues,
        StatsSeriesDefinition definition,
        StatsValueKind kind)
    {
        var rows = dailyValues
            .Where(row => string.Equals(row.SeriesKey, definition.Key, StringComparison.OrdinalIgnoreCase))
            .Select(row => (row.SeriesKey, row.Date, row.Value));

        var (periods, valuesByKey) = Bucket(query, rows);
        decimal[] values = valuesByKey.Values.FirstOrDefault() ?? new decimal[periods.Count];

        return new(
            definition.Key,
            definition.DisplayName,
            kind,
            [.. values.Select(v => StatsValues.Round(v, kind))],
            StatsValues.Round(values.Sum(), kind));
    }

    private static StatsTimeSeriesResult Create(StatsQuery query, IReadOnlyList<StatsPeriod> periods, IReadOnlyList<StatsTimeSeries> series) =>
        new(query.From, query.To, query.Grouping, query.ChannelId, periods, series, series.Sum(s => s.Total));

    /// <summary>
    /// Returns the period axis and the values per series key. Rows outside the range or with a count &lt;= 0 are ignored.
    /// </summary>
    private static (IReadOnlyList<StatsPeriod> Periods, Dictionary<string, int[]> ValuesByKey) Bucket(
        StatsQuery query,
        IEnumerable<StatsDailyCount> dailyCounts) =>
        Bucket(query, dailyCounts.Select(row => (row.SeriesKey, row.Date, row.Count)));

    /// <summary>
    /// Shared by count (<c>int</c>) and value (<c>decimal</c>) series.
    /// </summary>
    private static (IReadOnlyList<StatsPeriod> Periods, Dictionary<string, T[]> ValuesByKey) Bucket<T>(
        StatsQuery query,
        IEnumerable<(string SeriesKey, DateOnly Date, T Value)> rows)
        where T : struct, INumber<T>
    {
        var periods = StatsPeriods.Build(query.From, query.To, query.Grouping);
        var periodIndexes = periods
            .Select((period, index) => (period.Start, index))
            .ToDictionary(p => p.Start, p => p.index);

        var valuesByKey = new Dictionary<string, T[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var (SeriesKey, Date, Value) in rows)
        {
            if (Date < query.From || Date > query.To || Value <= T.Zero)
            {
                continue;
            }

            var periodStart = StatsPeriods.GetPeriodStart(Date, query.Grouping);
            if (!periodIndexes.TryGetValue(periodStart, out int index))
            {
                continue;
            }

            string key = SeriesKey ?? string.Empty;
            if (!valuesByKey.TryGetValue(key, out var values))
            {
                values = new T[periods.Count];
                valuesByKey[key] = values;
            }

            values[index] += Value;
        }

        return (periods, valuesByKey);
    }
}
