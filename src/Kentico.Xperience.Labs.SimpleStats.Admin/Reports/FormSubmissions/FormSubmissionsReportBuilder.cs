using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.FormSubmissions;

/// <summary>
/// Turns forms and daily submission counts into the form submissions report.
/// </summary>
internal static class FormSubmissionsReportBuilder
{
    /// <summary>
    /// Forms shown as their own series in the trend. The rest are summed into "Other".
    /// </summary>
    public const int MaxTrendSeries = 5;

    /// <summary>
    /// Builds the report.
    /// </summary>
    /// <param name="query">Normalized filter.</param>
    /// <param name="data">Forms and daily counts, from the start of the previous period (see <see cref="StatsComparison.GetPreviousRange"/>) to the end of the range.</param>
    /// <param name="getAdminPath">Returns the admin path of a form's submissions by form ID, or <c>null</c>.</param>
    public static FormSubmissionsResult Build(StatsQuery query, FormSubmissionsData data, Func<int, string?> getAdminPath)
    {
        var formsById = data.Forms
            .GroupBy(f => f.FormId)
            .ToDictionary(g => g.Key, g => g.First());

        // Rows of unknown forms (deleted since the metadata was read) are ignored.
        var dailyCounts = data.Rows
            .Where(row => formsById.ContainsKey(row.FormId))
            .Select(row => new StatsDailyCount(formsById[row.FormId].CodeName, row.Date, row.Count))
            .ToList();

        var displayNames = data.Forms
            .GroupBy(f => f.CodeName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().DisplayName, StringComparer.OrdinalIgnoreCase);

        var trend = StatsTimeSeriesBuilder.BuildDynamic(
            query,
            dailyCounts,
            key => displayNames.TryGetValue(key, out string? name) ? name : key,
            MaxTrendSeries);

        var totals = dailyCounts
            .Where(row => row.Date >= query.From && row.Date <= query.To && row.Count > 0)
            .GroupBy(row => row.SeriesKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(row => row.Count), StringComparer.OrdinalIgnoreCase);

        var entries = formsById.Values
            .Select(form => new StatsRankedEntry(
                Key: form.CodeName,
                Label: form.DisplayName,
                SecondaryLabel: null,
                Value: totals.TryGetValue(form.CodeName, out int total) ? total : 0,
                SecondaryValue: null,
                Url: null)
            {
                AdminPath = getAdminPath(form.FormId),
            })
            .ToList();

        var forms = StatsRankedBuilder.Build(
            query with { ChannelId = null },
            entries,
            trend.Total,
            entries.Count,
            limit: entries.Count,
            includeZero: true);

        // Rows before the range are the previous period; the trend and ranking above use only the range.
        var (previousFrom, _) = StatsComparison.GetPreviousRange(query);
        int previousTotal = dailyCounts
            .Where(row => row.Date >= previousFrom && row.Date < query.From && row.Count > 0)
            .Sum(row => row.Count);

        return new(
            trend,
            forms,
            trend.Total,
            StatsComparison.Create(query, trend.Total, previousTotal),
            forms.Items.Count,
            forms.Items.Count(i => i.Value > 0));
    }
}
