using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.FormSubmissions;

/// <summary>
/// Form submissions per form, read from the form data tables.
/// </summary>
/// <param name="Trend">
/// Submissions per period. One series per form with submissions (keyed by form code name), largest first.
/// The top <see cref="FormSubmissionsReportBuilder.MaxTrendSeries"/> forms are kept, the rest are summed into "Other".
/// </param>
/// <param name="Forms">
/// Every form, including forms with no submissions (listed last). <see cref="StatsRankedItem.AdminPath"/> links to the form's submissions.
/// </param>
/// <param name="Total">Submissions over all forms in the range.</param>
/// <param name="TotalComparison"><paramref name="Total"/> compared with the previous period of the same length.</param>
/// <param name="FormCount">Number of forms.</param>
/// <param name="FormsWithSubmissions">Number of forms with at least one submission in the range.</param>
public sealed record FormSubmissionsResult(
    StatsTimeSeriesResult Trend,
    StatsRankedResult Forms,
    int Total,
    StatsComparison TotalComparison,
    int FormCount,
    int FormsWithSubmissions)
{
    /// <summary>
    /// When the data was read from the database. Can be older than the request when served from cache.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Form metadata used by the report.
/// </summary>
/// <param name="FormId">Form ID.</param>
/// <param name="CodeName">Form code name. Used as series and item key.</param>
/// <param name="DisplayName">Form display name.</param>
/// <param name="TableName">Form data table name from the form's class. <c>null</c> when the class is missing.</param>
public sealed record FormDefinition(int FormId, string CodeName, string DisplayName, string? TableName);

/// <summary>
/// Submissions of one form on one day, as returned by the SQL aggregate.
/// </summary>
public sealed record FormDailyCount(int FormId, DateOnly Date, int Count);

/// <summary>
/// Forms and their daily submission counts. The rows can start before the report range (the previous period, used for the comparison).
/// </summary>
public sealed record FormSubmissionsData(IReadOnlyList<FormDefinition> Forms, IReadOnlyList<FormDailyCount> Rows)
{
    public static FormSubmissionsData Empty { get; } = new([], []);
}

/// <summary>
/// Form submissions data with the time it was read. This is the cached value.
/// </summary>
internal sealed record FormSubmissionsSnapshot(FormSubmissionsData Data, DateTimeOffset ReadAt);
