using System.Text.RegularExpressions;

using CMS.Base;

using Kentico.Xperience.Admin.Base.Authentication;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

using Microsoft.Extensions.Logging;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// Raises the <see cref="AfterExportStatsEvent"/> for the <c>LOG_EXPORT</c> command of the report pages.
/// </summary>
public interface IStatsExportEventPublisher
{
    /// <summary>
    /// Validates the request and invokes the registered <see cref="AfterExportStatsEvent"/> handlers.
    /// </summary>
    /// <returns><c>false</c> when the request is not valid and no event was raised.</returns>
    public Task<bool> Publish(Type reportPageType, StatsExportLogRequest? request, CancellationToken cancellationToken);
}

internal sealed partial class StatsExportEventPublisher(
    IEnumerable<IAsyncEventHandler<AfterExportStatsEvent>> handlers,
    IStatsUserIdAccessor userIdAccessor,
    TimeProvider clock,
    ILogger<StatsExportEventPublisher> logger) : IStatsExportEventPublisher
{
    internal const int MAX_EXPORT_NAME_LENGTH = 100;
    internal const int MAX_FILE_NAME_LENGTH = 255;

    private static readonly EventId exportEventId = new(0, "EXPORT");

    private readonly IEnumerable<IAsyncEventHandler<AfterExportStatsEvent>> handlers = handlers;
    private readonly IStatsUserIdAccessor userIdAccessor = userIdAccessor;
    private readonly TimeProvider clock = clock;
    private readonly ILogger<StatsExportEventPublisher> logger = logger;

    public async Task<bool> Publish(Type reportPageType, StatsExportLogRequest? request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reportPageType);

        if (request is null || !IsValidExportName(request.ExportName) || !IsValidFileName(request.FileName) || request.RowCount < 0)
        {
            logger.LogWarning(exportEventId, "Rejected an invalid CSV export log request of the '{ReportPageType}' page.", reportPageType.FullName);
            return false;
        }

        // Resolve once: the handlers are singletons, but the enumerable may be lazy.
        var handlerList = handlers.ToList();
        if (handlerList.Count == 0)
        {
            return true;
        }

        // Any exception here is a 500 and the admin shows an error toast, so an unknown user is logged as 0.
        int userId = 0;
        try
        {
            userId = await userIdAccessor.GetUserId();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exportEventId, exception, "Could not get the user of the CSV export of the '{ReportPageType}' page.", reportPageType.FullName);
        }

        var asyncEvent = new AfterExportStatsEvent(new StatsExportEventData
        {
            ReportPageTypeName = reportPageType.FullName ?? reportPageType.Name,
            UserID = userId,
            Timestamp = clock.GetLocalNow().DateTime,
            ExportName = request.ExportName!,
            FileName = request.FileName!,
            RowCount = request.RowCount,
        });

        // Each handler runs even if an earlier one fails (unlike InvokeAsync), and a failure never fails the export.
        foreach (var handler in handlerList)
        {
            try
            {
                await handler.HandleAsync(asyncEvent, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(
                    exportEventId,
                    exception,
                    "The '{Handler}' handler of the CSV export event of the '{ReportPageType}' page failed.",
                    handler.GetType().FullName,
                    reportPageType.FullName);
            }
        }

        return true;
    }

    internal static bool IsValidExportName(string? exportName) =>
        !string.IsNullOrEmpty(exportName)
        && exportName.Length <= MAX_EXPORT_NAME_LENGTH
        && ExportNamePattern().IsMatch(exportName);

    // A plain file name: no path, no characters Windows does not allow in file names, no control characters.
    internal static bool IsValidFileName(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && fileName.Length <= MAX_FILE_NAME_LENGTH
        && fileName != "."
        && fileName != ".."
        && !fileName.Any(c => char.IsControl(c) || "/\\:*?\"<>|".Contains(c));

    [GeneratedRegex(@"^[a-z0-9]+(?:-[a-z0-9]+)*\z", RegexOptions.CultureInvariant)]
    private static partial Regex ExportNamePattern();
}

/// <summary>
/// Returns the ID of the current administration user, or 0 when unknown. Kept separate so tests need no user objects
/// (they need the CMS container).
/// </summary>
internal interface IStatsUserIdAccessor
{
    public Task<int> GetUserId();
}

internal sealed class StatsUserIdAccessor(IAuthenticatedUserAccessor authenticatedUserAccessor) : IStatsUserIdAccessor
{
    private readonly IAuthenticatedUserAccessor authenticatedUserAccessor = authenticatedUserAccessor;

    public async Task<int> GetUserId() => (await authenticatedUserAccessor.Get())?.UserID ?? 0;
}
