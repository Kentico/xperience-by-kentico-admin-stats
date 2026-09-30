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
        Func<string, string> getDisplayName)
    {
        var (periods, valuesByKey) = Bucket(query, dailyCounts);

        var series = valuesByKey
            .Select(pair => new StatsTimeSeries(pair.Key, getDisplayName(pair.Key), pair.Value, pair.Value.Sum()))
            .OrderByDescending(s => s.Total)
            .ThenBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Create(query, periods, series);
    }

    private static StatsTimeSeriesResult Create(StatsQuery query, IReadOnlyList<StatsPeriod> periods, IReadOnlyList<StatsTimeSeries> series) =>
        new(query.From, query.To, query.Grouping, query.ChannelId, periods, series, series.Sum(s => s.Total));

    /// <summary>
    /// Returns the period axis and the values per series key. Rows outside the range or with a count &lt;= 0 are ignored.
    /// </summary>
    private static (IReadOnlyList<StatsPeriod> Periods, Dictionary<string, int[]> ValuesByKey) Bucket(
        StatsQuery query,
        IEnumerable<StatsDailyCount> dailyCounts)
    {
        var periods = StatsPeriods.Build(query.From, query.To, query.Grouping);
        var periodIndexes = periods
            .Select((period, index) => (period.Start, index))
            .ToDictionary(p => p.Start, p => p.index);

        var valuesByKey = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in dailyCounts)
        {
            if (row.Date < query.From || row.Date > query.To || row.Count <= 0)
            {
                continue;
            }

            var periodStart = StatsPeriods.GetPeriodStart(row.Date, query.Grouping);
            if (!periodIndexes.TryGetValue(periodStart, out int index))
            {
                continue;
            }

            string key = row.SeriesKey ?? string.Empty;
            if (!valuesByKey.TryGetValue(key, out int[]? values))
            {
                values = new int[periods.Count];
                valuesByKey[key] = values;
            }

            values[index] += row.Count;
        }

        return (periods, valuesByKey);
    }
}
