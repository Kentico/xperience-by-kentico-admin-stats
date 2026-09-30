using Kentico.Xperience.AdminStats.Shared;

namespace Kentico.Xperience.AdminStats.Reports.NewContacts;

/// <summary>
/// Turns daily contact counts into a period-bucketed, zero-filled report.
/// </summary>
internal static class NewContactsReportBuilder
{
    public const string IdentifiedKey = "identified";
    public const string AnonymousKey = "anonymous";

    /// <summary>
    /// Fixed series order: identified first, then anonymous. Not sorted by total.
    /// </summary>
    private static readonly StatsSeriesDefinition[] series =
    [
        new(IdentifiedKey, "Identified"),
        new(AnonymousKey, "Anonymous"),
    ];

    public static NewContactsResult Build(StatsQuery query, IEnumerable<NewContactsDailyCount> dailyCounts)
    {
        var rows = dailyCounts.SelectMany(row => new StatsDailyCount[]
        {
            new(IdentifiedKey, row.Date, row.Identified),
            new(AnonymousKey, row.Date, row.Anonymous),
        });

        var trend = StatsTimeSeriesBuilder.BuildFixed(query, rows, series);

        int identified = trend.Series[0].Total;
        int anonymous = trend.Series[1].Total;
        int total = identified + anonymous;

        return new(trend, identified, anonymous, total > 0 ? (double)identified / total : 0);
    }
}
