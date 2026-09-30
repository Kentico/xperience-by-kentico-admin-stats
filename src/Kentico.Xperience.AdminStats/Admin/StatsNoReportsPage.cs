using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.AdminStats.Admin;

[assembly: UIPage(
    uiPageType: typeof(StatsNoReportsPage),
    parentType: typeof(StatsApplicationPage),
    slug: "no-reports",
    name: "No reports available",
    templateName: StatsNoReportsPage.TEMPLATE_NAME,
    order: StatsNoReportsPage.ORDER)]

namespace Kentico.Xperience.AdminStats.Admin;

/// <summary>
/// Hidden page shown when the user may open the application but no report.
/// It is the last child of the application, so the default route lands on it only when all sections are denied.
/// Sections without an allowed report redirect to it.
/// </summary>
[UINavigation(false)]
public sealed class StatsNoReportsPage : Page
{
    public const string TEMPLATE_NAME = "@kentico/xperience-admin-stats/NoReports";

    /// <summary>
    /// Order after all sections.
    /// </summary>
    public const int ORDER = int.MaxValue;
}
