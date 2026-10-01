using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TopPages;

/// <summary>
/// Turns top page rows into a ranked result.
/// </summary>
internal static class TopPagesReportBuilder
{
    internal const string NoUrlLabel = "(no URL)";

    public static StatsRankedResult Build(StatsQuery query, TopPagesData data, int limit)
    {
        var entries = data.Rows.Select(row => new StatsRankedEntry(
            Key: row.Url,
            Label: string.IsNullOrWhiteSpace(row.Url) ? NoUrlLabel : row.Url,
            // Stored as logged (for example "Page visit 'Home'"); shown as extra text, never parsed.
            SecondaryLabel: string.IsNullOrWhiteSpace(row.Title) ? null : row.Title,
            Value: row.Visits,
            SecondaryValue: row.Contacts,
            Url: GetPublicUrl(row.Url)));

        return StatsRankedBuilder.Build(query, entries, data.TotalVisits, data.PageCount, limit);
    }

    /// <summary>
    /// Returns the URL when it is an absolute http(s) URL that can be opened in a new tab.
    /// </summary>
    internal static string? GetPublicUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? url
            : null;
}
