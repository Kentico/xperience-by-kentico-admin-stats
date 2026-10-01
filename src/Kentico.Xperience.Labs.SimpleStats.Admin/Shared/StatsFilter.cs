namespace Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

/// <summary>
/// Filter sent by the admin client. Missing or invalid values fall back to defaults in <see cref="Normalize"/>.
/// </summary>
public sealed record StatsFilter
{
    public const int DefaultRangeDays = 30;

    /// <summary>
    /// Longest allowed range (about 5 years). Longer ranges are shortened from the start.
    /// </summary>
    public const int MaxRangeDays = 1827;

    /// <summary>
    /// First day of the range (inclusive).
    /// </summary>
    public DateOnly? From { get; init; }

    /// <summary>
    /// Last day of the range (inclusive).
    /// </summary>
    public DateOnly? To { get; init; }

    public StatsGrouping? Grouping { get; init; }

    /// <summary>
    /// Optional channel ID. <c>null</c> or a value &lt;= 0 means all channels.
    /// </summary>
    public int? ChannelId { get; init; }

    /// <summary>
    /// Applies defaults and limits and returns a query that is safe to run.
    /// </summary>
    /// <param name="today">Current date used for the default range.</param>
    public StatsQuery Normalize(DateOnly today)
    {
        var to = To ?? today;
        var from = From ?? to.AddDays(-(DefaultRangeDays - 1));

        if (from > to)
        {
            (from, to) = (to, from);
        }

        if (to.DayNumber - from.DayNumber + 1 > MaxRangeDays)
        {
            from = to.AddDays(-(MaxRangeDays - 1));
        }

        var grouping = Grouping is { } g && Enum.IsDefined(g) ? g : StatsGrouping.Day;
        int? channelId = ChannelId is > 0 ? ChannelId : null;

        return new(from, to, grouping, channelId);
    }
}

/// <summary>
/// Normalized filter used by report queries.
/// </summary>
/// <param name="From">First day of the range (inclusive).</param>
/// <param name="To">Last day of the range (inclusive).</param>
/// <param name="Grouping">Period size.</param>
/// <param name="ChannelId">Optional channel ID.</param>
public sealed record StatsQuery(DateOnly From, DateOnly To, StatsGrouping Grouping, int? ChannelId);
