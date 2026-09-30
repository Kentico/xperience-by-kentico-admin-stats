namespace Kentico.Xperience.AdminStats.Shared;

/// <summary>
/// A value in the selected range compared with the same value in the previous period.
/// </summary>
/// <param name="Current">Value in the selected range.</param>
/// <param name="Previous">Value in the previous period.</param>
/// <param name="Change">
/// Relative change as a ratio (0.12 = +12%, -0.5 = -50%). <c>null</c> when <paramref name="Previous"/> is 0.
/// </param>
/// <param name="PreviousFrom">First day of the previous period (inclusive).</param>
/// <param name="PreviousTo">Last day of the previous period (inclusive).</param>
public sealed record StatsComparison(int Current, int Previous, double? Change, DateOnly PreviousFrom, DateOnly PreviousTo)
{
    /// <summary>
    /// Returns the previous period: the same number of days, ending the day before <see cref="StatsQuery.From"/>.
    /// For example, Sep 1–30 (30 days) returns Aug 2–31.
    /// </summary>
    /// <remarks>Clamped to <see cref="DateOnly.MinValue"/>, so a range that starts there has a shorter (or empty) previous period.</remarks>
    public static (DateOnly From, DateOnly To) GetPreviousRange(StatsQuery query)
    {
        int days = query.To.DayNumber - query.From.DayNumber + 1;
        int toDay = Math.Max(query.From.DayNumber - 1, 0);
        int fromDay = Math.Max(query.From.DayNumber - days, 0);

        return (DateOnly.FromDayNumber(fromDay), DateOnly.FromDayNumber(toDay));
    }

    /// <summary>
    /// Creates the comparison for the query's range and its previous period (see <see cref="GetPreviousRange"/>).
    /// </summary>
    public static StatsComparison Create(StatsQuery query, int current, int previous)
    {
        var (from, to) = GetPreviousRange(query);
        double? change = previous == 0 ? null : (double)(current - previous) / previous;

        return new(current, previous, change, from, to);
    }
}
