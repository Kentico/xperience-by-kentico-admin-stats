using Kentico.Xperience.AdminStats.Shared;

namespace Kentico.Xperience.AdminStats.Reports.ActivityCounts;

/// <summary>
/// Turns daily SQL aggregates into a period-bucketed, zero-filled report.
/// </summary>
internal static class ActivityCountsReportBuilder
{
    public static ActivityCountsResult Build(
        StatsQuery query,
        IEnumerable<ActivityDailyCount> dailyCounts,
        IReadOnlyDictionary<string, string> displayNames)
    {
        var periods = StatsPeriods.Build(query.From, query.To, query.Grouping);
        var periodIndexes = periods
            .Select((period, index) => (period.Start, index))
            .ToDictionary(p => p.Start, p => p.index);

        var valuesByType = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);

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

            string activityType = row.ActivityType ?? string.Empty;
            if (!valuesByType.TryGetValue(activityType, out int[]? values))
            {
                values = new int[periods.Count];
                valuesByType[activityType] = values;
            }

            values[index] += row.Count;
        }

        var series = valuesByType
            .Select(pair => new ActivityCountsSeries(
                pair.Key,
                GetDisplayName(pair.Key, displayNames),
                pair.Value,
                pair.Value.Sum()))
            .OrderByDescending(s => s.Total)
            .ThenBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new(
            query.From,
            query.To,
            query.Grouping,
            query.ChannelId,
            periods,
            series,
            series.Sum(s => s.Total));
    }

    private static string GetDisplayName(string activityType, IReadOnlyDictionary<string, string> displayNames)
    {
        if (displayNames.TryGetValue(activityType, out string? name) && !string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return string.IsNullOrWhiteSpace(activityType) ? "(no type)" : activityType;
    }
}
