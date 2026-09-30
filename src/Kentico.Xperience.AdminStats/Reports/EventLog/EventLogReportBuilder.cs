using Kentico.Xperience.AdminStats.Shared;

namespace Kentico.Xperience.AdminStats.Reports.EventLog;

/// <summary>
/// Turns aggregated event log data into the event log report.
/// </summary>
internal static class EventLogReportBuilder
{
    /// <summary>
    /// Rows of each top list (sources, event codes, users).
    /// </summary>
    public const int TopLimit = 10;

    /// <summary>
    /// Key of the row of events without a user. Contains characters user keys cannot start with, so it does not collide.
    /// </summary>
    public const string SystemUserKey = "(system)";

    public const string SystemUserLabel = "System";

    /// <summary>
    /// Builds the report.
    /// </summary>
    /// <param name="query">Normalized filter.</param>
    /// <param name="data">Data from the start of the previous period (see <see cref="StatsComparison.GetPreviousRange"/>) to the end of the range.</param>
    /// <param name="logSizeLimit">The "Event log size" setting, or <c>null</c>.</param>
    /// <param name="getUserPath">Returns the admin path of a user by ID, or <c>null</c>.</param>
    public static EventLogResult Build(EventLogQuery query, EventLogReportData data, int? logSizeLimit, Func<int, string?> getUserPath)
    {
        var range = query.Range with { ChannelId = null };
        var (previousFrom, previousTo) = StatsComparison.GetPreviousRange(range);

        var dailyCounts = data.Daily
            .Where(row => row.Count > 0)
            .Select(row => new StatsDailyCount(row.EventType, row.Date, row.Count))
            .ToList();

        var definitions = query.EventType is null
            ? EventLogTypes.All
            : EventLogTypes.All.Where(t => t.Key == query.EventType).ToList();

        // Rows before the range are the previous period; the trend uses only the range.
        var trend = StatsTimeSeriesBuilder.BuildFixed(range, dailyCounts, definitions);

        int Sum(string? type, DateOnly from, DateOnly to) =>
            dailyCounts
                .Where(row => row.Date >= from && row.Date <= to)
                .Where(row => type is null
                    ? EventLogTypes.All.Any(t => string.Equals(t.Key, row.SeriesKey, StringComparison.OrdinalIgnoreCase))
                    : string.Equals(row.SeriesKey, type, StringComparison.OrdinalIgnoreCase))
                .Sum(row => row.Count);

        var totals = EventLogTypes.All
            .Select(t => new EventLogTypeTotal(
                t.Key,
                t.DisplayName,
                StatsComparison.Create(range, Sum(t.Key, range.From, range.To), Sum(t.Key, previousFrom, previousTo))))
            .ToList();

        var totalComparison = StatsComparison.Create(range, Sum(null, range.From, range.To), Sum(null, previousFrom, previousTo));

        // Top lists have the type filter applied, so their share is of the filtered total.
        int filteredTotal = Sum(query.EventType, range.From, range.To);

        return new(
            query.EventType,
            trend,
            totals,
            totalComparison,
            BuildGroups(range, data.Sources, filteredTotal),
            BuildGroups(range, data.XperienceSources, filteredTotal),
            BuildGroups(range, data.CustomSources, filteredTotal),
            BuildGroups(range, data.Codes, filteredTotal),
            BuildUsers(range, data.Users, filteredTotal, getUserPath),
            logSizeLimit);
    }

    private static StatsRankedResult BuildGroups(StatsQuery range, EventLogTop<EventLogGroupRow> top, int total)
    {
        var entries = top.Rows.Select(row => new StatsRankedEntry(
            Key: row.Key,
            Label: row.Key,
            SecondaryLabel: null,
            Value: row.Current,
            SecondaryValue: null,
            Url: null)
        {
            PreviousValue = Math.Max(row.Previous, 0),
        });

        return StatsRankedBuilder.Build(range, entries, total, top.GroupCount, TopLimit);
    }

    private static StatsRankedResult BuildUsers(StatsQuery range, EventLogTop<EventLogUserRow> top, int total, Func<int, string?> getUserPath)
    {
        var entries = top.Rows.Select(row =>
        {
            if (row.UserId is int userId)
            {
                return new StatsRankedEntry($"user:{userId}", row.UserName ?? $"User {userId}", null, row.Current, null, null)
                {
                    AdminPath = row.UserExists ? getUserPath(userId) : null,
                    PreviousValue = Math.Max(row.Previous, 0),
                };
            }

            // Events without a user ID: system events (no name), or a name logged without an ID.
            var entry = row.UserName is string name
                ? new StatsRankedEntry($"name:{name}", name, null, row.Current, null, null)
                : new StatsRankedEntry(SystemUserKey, SystemUserLabel, null, row.Current, null, null);
            return entry with { PreviousValue = Math.Max(row.Previous, 0) };
        });

        return StatsRankedBuilder.Build(range, entries, total, top.GroupCount, TopLimit);
    }
}
