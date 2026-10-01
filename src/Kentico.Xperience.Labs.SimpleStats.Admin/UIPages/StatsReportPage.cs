using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// Base of the "Stats (Labs)" report pages. Sends the permissions shared by all reports to the client and logs CSV exports.
/// </summary>
public abstract class StatsReportPage<TClientProperties>(
    IUIPermissionEvaluator permissionEvaluator,
    IStatsExportEventPublisher exportEventPublisher) : Page<TClientProperties>
    where TClientProperties : StatsReportClientProperties, new()
{
    private readonly IUIPermissionEvaluator permissionEvaluator = permissionEvaluator;
    private readonly IStatsExportEventPublisher exportEventPublisher = exportEventPublisher;

    public sealed override async Task<TClientProperties> ConfigureTemplateProperties(TClientProperties properties)
    {
        // UI guard only: the client builds the CSV from data already on the page.
        properties.CanExport = (await permissionEvaluator.Evaluate(StatsPermissions.EXPORT)).Succeeded;

        return await ConfigureReportProperties(properties);
    }

    /// <summary>
    /// Raises the <see cref="AfterExportStatsEvent"/> after the client downloaded a CSV. The page's own
    /// <see cref="UIEvaluatePermissionAttribute"/> is checked before any command, so this needs both permissions.
    /// </summary>
    [PageCommand(CommandName = "LOG_EXPORT", Permission = StatsPermissions.EXPORT)]
    public async Task<ICommandResponse> LogExport(StatsExportLogRequest request, CancellationToken cancellationToken)
    {
        // The client ignores the response; an invalid request is logged as a warning by the publisher.
        await exportEventPublisher.Publish(GetType(), request, cancellationToken);

        return Response();
    }

    /// <summary>
    /// Sets the report-specific client properties.
    /// </summary>
    protected abstract Task<TClientProperties> ConfigureReportProperties(TClientProperties properties);
}

/// <summary>
/// Client properties shared by all report pages.
/// </summary>
public abstract class StatsReportClientProperties : TemplateClientProperties
{
    /// <summary>
    /// Whether the user has the <see cref="StatsPermissions.EXPORT"/> permission. The client hides "Export CSV" when not.
    /// </summary>
    public bool CanExport { get; set; }
}
