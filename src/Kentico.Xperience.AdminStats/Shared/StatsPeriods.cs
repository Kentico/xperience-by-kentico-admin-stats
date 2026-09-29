using System.Globalization;

namespace Kentico.Xperience.AdminStats.Shared;

/// <summary>
/// One period (bucket) on a trend axis.
/// </summary>
/// <param name="Start">First day of the period.</param>
/// <param name="Label">Short label for axes, tables and CSV.</param>
public sealed record StatsPeriod(DateOnly Start, string Label);

/// <summary>
/// Builds continuous period axes and maps dates to periods.
/// </summary>
public static class StatsPeriods
{
    /// <summary>
    /// Returns the first day of the period that contains <paramref name="date"/>.
    /// Weeks start on Monday (ISO 8601).
    /// </summary>
    public static DateOnly GetPeriodStart(DateOnly date, StatsGrouping grouping) =>
        grouping switch
        {
            StatsGrouping.Day => date,
            StatsGrouping.Week => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)),
            StatsGrouping.Month => new DateOnly(date.Year, date.Month, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(grouping), grouping, null),
        };

    /// <summary>
    /// Returns every period that overlaps the range, in order, with no gaps.
    /// The first and last period can be partial.
    /// </summary>
    public static IReadOnlyList<StatsPeriod> Build(DateOnly from, DateOnly to, StatsGrouping grouping)
    {
        if (to < from)
        {
            return [];
        }

        bool multiYear = from.Year != to.Year;
        var periods = new List<StatsPeriod>();

        for (var start = GetPeriodStart(from, grouping); start <= to; start = Next(start, grouping))
        {
            periods.Add(new(start, FormatLabel(start, grouping, multiYear)));
        }

        return periods;
    }

    private static DateOnly Next(DateOnly start, StatsGrouping grouping) =>
        grouping switch
        {
            StatsGrouping.Day => start.AddDays(1),
            StatsGrouping.Week => start.AddDays(7),
            StatsGrouping.Month => start.AddMonths(1),
            _ => throw new ArgumentOutOfRangeException(nameof(grouping), grouping, null),
        };

    private static string FormatLabel(DateOnly start, StatsGrouping grouping, bool multiYear) =>
        grouping switch
        {
            StatsGrouping.Month => start.ToString("MMM yyyy", CultureInfo.InvariantCulture),
            StatsGrouping.Day or StatsGrouping.Week => start.ToString(multiYear ? "MMM d, yyyy" : "MMM d", CultureInfo.InvariantCulture),
            _ => throw new ArgumentOutOfRangeException(nameof(grouping), grouping, null),
        };
}
