using Kentico.Xperience.Admin.Base;

namespace Kentico.Xperience.AdminStats.Admin;

/// <summary>
/// Base of the "Stats (Labs)" report pages. Sends the permissions shared by all reports to the client.
/// </summary>
public abstract class StatsReportPage<TClientProperties>(IUIPermissionEvaluator permissionEvaluator) : Page<TClientProperties>
    where TClientProperties : StatsReportClientProperties, new()
{
    private readonly IUIPermissionEvaluator permissionEvaluator = permissionEvaluator;

    public sealed override async Task<TClientProperties> ConfigureTemplateProperties(TClientProperties properties)
    {
        // UI guard only: the client builds the CSV from data already on the page.
        properties.CanExport = (await permissionEvaluator.Evaluate(StatsPermissions.EXPORT)).Succeeded;

        return await ConfigureReportProperties(properties);
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
