namespace Kentico.Xperience.AdminStats.Shared;

/// <summary>
/// One row of a ranked list (top pages, top referrers, forms by submissions, ...).
/// </summary>
/// <param name="Rank">1-based position in the list.</param>
/// <param name="Key">Stable identifier of the item (for example the URL). Unique in the list.</param>
/// <param name="Label">Primary label shown in charts and tables.</param>
/// <param name="SecondaryLabel">Optional extra text shown under or next to the label.</param>
/// <param name="Value">Ranked value (for example visits).</param>
/// <param name="SecondaryValue">Optional second number (for example unique contacts).</param>
/// <param name="Share">Share of <see cref="StatsRankedResult.Total"/> (0-1).</param>
/// <param name="Url">Optional absolute link opened in a new tab.</param>
public sealed record StatsRankedItem(
    int Rank,
    string Key,
    string Label,
    string? SecondaryLabel,
    int Value,
    int? SecondaryValue,
    double Share,
    string? Url);

/// <summary>
/// Ranked list for one range and channel.
/// </summary>
/// <param name="From">Applied range start (inclusive).</param>
/// <param name="To">Applied range end (inclusive).</param>
/// <param name="ChannelId">Applied channel filter.</param>
/// <param name="Items">Top items, largest value first.</param>
/// <param name="Total">Sum of values over all items in the range, not only <paramref name="Items"/>.</param>
/// <param name="ItemCount">Number of distinct items in the range, not only <paramref name="Items"/>.</param>
public sealed record StatsRankedResult(
    DateOnly From,
    DateOnly To,
    int? ChannelId,
    IReadOnlyList<StatsRankedItem> Items,
    int Total,
    int ItemCount)
{
    /// <summary>
    /// When the data was read from the database. Can be older than the request when served from cache.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Unranked input row for <see cref="StatsRankedBuilder"/>.
/// </summary>
public sealed record StatsRankedEntry(
    string Key,
    string Label,
    string? SecondaryLabel,
    int Value,
    int? SecondaryValue,
    string? Url);

/// <summary>
/// Orders entries, applies the limit and computes shares.
/// </summary>
public static class StatsRankedBuilder
{
    /// <summary>
    /// Builds a ranked result.
    /// </summary>
    /// <param name="query">Normalized filter.</param>
    /// <param name="entries">Entries (usually already top N from SQL). Entries with a value &lt;= 0 are dropped; duplicate keys keep the first.</param>
    /// <param name="total">Sum of values over all items in the range. Raised to the sum of <paramref name="entries"/> if lower.</param>
    /// <param name="itemCount">Number of distinct items in the range. Raised to the number of entries if lower.</param>
    /// <param name="limit">Maximum number of items.</param>
    public static StatsRankedResult Build(
        StatsQuery query,
        IEnumerable<StatsRankedEntry> entries,
        int total,
        int itemCount,
        int limit)
    {
        var valid = entries
            .Where(e => e.Value > 0)
            .DistinctBy(e => e.Key, StringComparer.Ordinal)
            .ToList();

        int safeTotal = Math.Max(total, valid.Sum(e => e.Value));
        int safeCount = Math.Max(itemCount, valid.Count);

        var items = valid
            .OrderByDescending(e => e.Value)
            .ThenBy(e => e.Key, StringComparer.Ordinal)
            .Take(Math.Max(limit, 0))
            .Select((e, index) => new StatsRankedItem(
                index + 1,
                e.Key,
                e.Label,
                e.SecondaryLabel,
                e.Value,
                e.SecondaryValue,
                safeTotal > 0 ? (double)e.Value / safeTotal : 0,
                e.Url))
            .ToList();

        return new(query.From, query.To, query.ChannelId, items, safeTotal, safeCount);
    }
}
