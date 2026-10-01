namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TopPages;

/// <summary>
/// Visits of one page URL, as returned by the SQL aggregate.
/// </summary>
/// <param name="Url">Visited URL without query string and fragment. Empty when not recorded.</param>
/// <param name="Visits">Number of page visit activities.</param>
/// <param name="Contacts">Number of distinct contacts that visited the URL.</param>
/// <param name="Title">Activity title as stored (for example <c>Page visit 'Home'</c>). Not parsed.</param>
public sealed record TopPageRow(string Url, int Visits, int Contacts, string? Title);

/// <summary>
/// Top page rows plus range totals for one range and channel.
/// </summary>
/// <param name="Rows">Top N rows, most visits first.</param>
/// <param name="TotalVisits">Page visits over all URLs in the range.</param>
/// <param name="PageCount">Distinct URLs in the range.</param>
public sealed record TopPagesData(IReadOnlyList<TopPageRow> Rows, int TotalVisits, int PageCount)
{
    public static TopPagesData Empty { get; } = new([], 0, 0);
}

/// <summary>
/// Top pages data with the time it was read. This is the cached value.
/// </summary>
internal sealed record TopPagesSnapshot(TopPagesData Data, DateTimeOffset ReadAt);
