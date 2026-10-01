namespace Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

/// <summary>
/// Input of the shared <c>LOG_EXPORT</c> page command, sent by the client after a CSV download.
/// </summary>
public sealed record StatsExportLogRequest
{
    /// <summary>
    /// Stable ID of the exported tile (lowercase letters, digits and hyphens), for example <c>consents-events</c>.
    /// </summary>
    public string? ExportName { get; init; }

    /// <summary>
    /// Name of the downloaded file.
    /// </summary>
    public string? FileName { get; init; }

    /// <summary>
    /// Number of data rows, not counting the header.
    /// </summary>
    public int RowCount { get; init; }
}
