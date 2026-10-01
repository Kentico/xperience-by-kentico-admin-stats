using CMS.EventLog;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EventLog;

/// <summary>
/// Filter of the event log report, sent by the admin client: the shared range and grouping (<see cref="StatsFilter"/>)
/// plus an optional event type. The shared filter is wrapped, not changed, so reports 01–05 keep their filter and cache keys.
/// </summary>
public sealed record EventLogFilter
{
    /// <summary>
    /// Range and grouping. The channel is ignored (events have no channel). <c>null</c> means defaults.
    /// </summary>
    public StatsFilter? Range { get; init; }

    /// <summary>
    /// Optional event type code (<see cref="CMS.EventLog.EventType"/>: <c>I</c>, <c>W</c>, <c>E</c>).
    /// <c>null</c> or an unknown value means all types.
    /// </summary>
    public string? EventType { get; init; }

    /// <summary>
    /// Applies defaults and limits and returns a query that is safe to run.
    /// </summary>
    /// <param name="today">Current date used for the default range.</param>
    public EventLogQuery Normalize(DateOnly today) =>
        new((Range ?? new StatsFilter()).Normalize(today) with { ChannelId = null }, EventLogTypes.Normalize(EventType));
}

/// <summary>
/// Normalized event log filter.
/// </summary>
/// <param name="Range">Range and grouping. <see cref="StatsQuery.ChannelId"/> is always <c>null</c>.</param>
/// <param name="EventType">One of <see cref="EventLogTypes.All"/>, or <c>null</c> for all types.</param>
public sealed record EventLogQuery(StatsQuery Range, string? EventType);

/// <summary>
/// Input of the event log <c>LOAD</c> page command.
/// </summary>
public sealed record EventLogLoadRequest
{
    /// <summary>
    /// Report filter. <c>null</c> means defaults.
    /// </summary>
    public EventLogFilter? Filter { get; init; }

    /// <summary>
    /// When <c>true</c>, cached data for the filter is dropped and read again from the database.
    /// </summary>
    public bool Refresh { get; init; }
}

/// <summary>
/// Event types of the event log, in trend order (information, warnings, errors).
/// Codes are the product constants (<see cref="EventType"/>), stored in <c>CMS_EventLog.EventType</c>.
/// </summary>
public static class EventLogTypes
{
    /// <summary>
    /// Event types and their display names, most common first. Used as the fixed trend series:
    /// the first series is the bottom of each stacked column, so information is at the bottom and errors on top.
    /// </summary>
    public static IReadOnlyList<StatsSeriesDefinition> All { get; } =
    [
        new(EventType.INFORMATION, "Information"),
        new(EventType.WARNING, "Warnings"),
        new(EventType.ERROR, "Errors"),
    ];

    /// <summary>
    /// Returns the type code in the product casing, or <c>null</c> for an empty or unknown value.
    /// </summary>
    public static string? Normalize(string? eventType) =>
        string.IsNullOrWhiteSpace(eventType)
            ? null
            : All.FirstOrDefault(t => string.Equals(t.Key, eventType.Trim(), StringComparison.OrdinalIgnoreCase))?.Key;
}

/// <summary>
/// Events of one type in the range compared with the previous period.
/// </summary>
/// <param name="EventType">Type code (<c>I</c>, <c>W</c>, <c>E</c>).</param>
/// <param name="DisplayName">Type display name.</param>
/// <param name="Comparison">Count in the range vs the previous period.</param>
public sealed record EventLogTypeTotal(string EventType, string DisplayName, StatsComparison Comparison);

/// <summary>
/// Event log report.
/// </summary>
/// <param name="EventType">Applied event type filter, <c>null</c> for all types.</param>
/// <param name="Trend">Events per period, one fixed series per type (information, warnings, errors; bottom to top in the stack). With a type filter, only that type.</param>
/// <param name="Totals">Every type in fixed order, compared with the previous period. Not affected by the type filter.</param>
/// <param name="TotalComparison">All events (all types) in the range vs the previous period.</param>
/// <param name="TopSources">Sources with the most events in the range (type filter applied), with the previous period value and change.</param>
/// <param name="TopXperienceSources">Like <paramref name="TopSources"/>, only sources logged by Xperience (<see cref="EventLogSources.XperiencePrefixes"/>).</param>
/// <param name="TopCustomSources">Like <paramref name="TopSources"/>, only other (custom) sources.</param>
/// <param name="TopCodes">Event codes with the most events in the range (type filter applied), with the previous period value and change.</param>
/// <param name="TopUsers">Users with the most events in the range (type filter applied), with the previous period value and change. Events without a user are one "System" row.</param>
/// <param name="LogSizeLimit">
/// The "Event log size" setting (<c>CMSLogSize</c>): the maximum number of events kept. Older events are deleted.
/// 0 means events are not logged. <c>null</c> when the setting has no valid value.
/// </param>
public sealed record EventLogResult(
    string? EventType,
    StatsTimeSeriesResult Trend,
    IReadOnlyList<EventLogTypeTotal> Totals,
    StatsComparison TotalComparison,
    StatsRankedResult TopSources,
    StatsRankedResult TopXperienceSources,
    StatsRankedResult TopCustomSources,
    StatsRankedResult TopCodes,
    StatsRankedResult TopUsers,
    int? LogSizeLimit)
{
    /// <summary>
    /// Path of the native Event log application's listing, relative to the admin root. <c>null</c> when it is not available.
    /// </summary>
    public string? EventLogPath { get; init; }

    /// <summary>
    /// When the data was read from the database. Can be older than the request when served from cache.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Events of one type on one day, as returned by the SQL aggregate.
/// </summary>
public sealed record EventLogDailyCount(string EventType, DateOnly Date, int Count);

/// <summary>
/// Events of one source or event code in the range and in the previous period.
/// </summary>
public sealed record EventLogGroupRow(string Key, int Current, int Previous);

/// <summary>
/// Events of one user in the range and in the previous period.
/// </summary>
/// <param name="UserId">User ID. <c>null</c> for events without a user.</param>
/// <param name="UserName">User name as logged. <c>null</c> when empty.</param>
/// <param name="UserExists">Whether the user still exists (for the admin link).</param>
/// <param name="Current">Events in the range.</param>
/// <param name="Previous">Events in the previous period.</param>
public sealed record EventLogUserRow(int? UserId, string? UserName, bool UserExists, int Current, int Previous);

/// <summary>
/// Top N rows of one grouping and the number of all groups with events in the range.
/// </summary>
public sealed record EventLogTop<TRow>(IReadOnlyList<TRow> Rows, int GroupCount)
{
    public static EventLogTop<TRow> Empty { get; } = new([], 0);
}

/// <summary>
/// Aggregated event log data. <see cref="Daily"/> starts at the previous period (all types);
/// the top lists have the type filter applied.
/// </summary>
public sealed record EventLogReportData(
    IReadOnlyList<EventLogDailyCount> Daily,
    EventLogTop<EventLogGroupRow> Sources,
    EventLogTop<EventLogGroupRow> Codes,
    EventLogTop<EventLogUserRow> Users)
{
    /// <summary>
    /// Top sources that start with one of <see cref="EventLogSources.XperiencePrefixes"/>.
    /// </summary>
    public EventLogTop<EventLogGroupRow> XperienceSources { get; init; } = EventLogTop<EventLogGroupRow>.Empty;

    /// <summary>
    /// Top sources that do not start with one of <see cref="EventLogSources.XperiencePrefixes"/>.
    /// </summary>
    public EventLogTop<EventLogGroupRow> CustomSources { get; init; } = EventLogTop<EventLogGroupRow>.Empty;

    public static EventLogReportData Empty { get; } = new([], EventLogTop<EventLogGroupRow>.Empty, EventLogTop<EventLogGroupRow>.Empty, EventLogTop<EventLogUserRow>.Empty);
}

/// <summary>
/// Event log data and log size setting with the time it was read. This is the cached value.
/// </summary>
internal sealed record EventLogReportSnapshot(EventLogReportData Data, int? LogSizeLimit, DateTimeOffset ReadAt);
