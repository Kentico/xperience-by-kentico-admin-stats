using CMS.Base;

namespace Kentico.Xperience.AdminStats.Admin;

/// <summary>
/// Raised after a user exported a report tile to CSV ("Export CSV").
/// </summary>
/// <remarks>
/// The CSV is built in the browser from data the user can already see. The client reports the export through
/// the <c>LOG_EXPORT</c> page command after the download starts, so the event is an audit signal, not a guarantee.
/// A handler exception is logged and does not fail the command.
/// </remarks>
public sealed class AfterExportStatsEvent : AsyncEvent<StatsExportEventData>
{
    /// <summary>
    /// Creates an event with empty data. Use it only in tests.
    /// </summary>
    public AfterExportStatsEvent()
    {
    }

    /// <summary>
    /// Creates an event with the specified data.
    /// </summary>
    public AfterExportStatsEvent(StatsExportEventData data)
        : base(data)
    {
    }
}

/// <summary>
/// Data of the <see cref="AfterExportStatsEvent"/>.
/// </summary>
public sealed class StatsExportEventData
{
    /// <summary>
    /// Full type name of the report page, for example <c>Kentico.Xperience.AdminStats.Admin.ConsentsPage</c>.
    /// </summary>
    public string ReportPageTypeName { get; init; } = string.Empty;

    /// <summary>
    /// Identifier of the administration user who exported the data, or 0 when the user could not be determined.
    /// </summary>
    public int UserID { get; init; }

    /// <summary>
    /// Date and time of the export, in the server local time.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Stable ID of the exported tile, for example <c>consents-events</c>. Does not change with the filter.
    /// </summary>
    public string ExportName { get; init; } = string.Empty;

    /// <summary>
    /// Name of the downloaded file, for example <c>consents-events_2026-09-01_2026-09-30_day.csv</c>.
    /// </summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>
    /// Number of data rows in the file, not counting the header.
    /// </summary>
    public int RowCount { get; init; }
}
