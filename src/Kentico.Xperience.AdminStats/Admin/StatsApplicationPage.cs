using CMS.Membership;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.UIPages;
using Kentico.Xperience.AdminStats.Admin;

[assembly: UIApplication(
    identifier: StatsApplicationPage.IDENTIFIER,
    type: typeof(StatsApplicationPage),
    slug: "admin-stats",
    name: "Stats (Labs)",
    category: BaseApplicationCategories.DIGITAL_MARKETING,
    icon: Icons.Graph,
    templateName: TemplateNames.SECTION_LAYOUT)]

namespace Kentico.Xperience.AdminStats.Admin;

/// <summary>
/// "Stats (Labs)" admin application. Each report is a child page.
/// Assign the View permission to roles that may open the application,
/// plus one <see cref="StatsPermissions"/> permission per report the role may see.
/// </summary>
[UIPermission(SystemPermissions.VIEW)]
[UIPermission(StatsPermissions.ACTIVITY_COUNTS, StatsPermissions.ACTIVITY_COUNTS_DISPLAY_NAME)]
[UIPermission(StatsPermissions.TOP_PAGES, StatsPermissions.TOP_PAGES_DISPLAY_NAME)]
[UIPermission(StatsPermissions.NEW_CONTACTS, StatsPermissions.NEW_CONTACTS_DISPLAY_NAME)]
[UIPermission(StatsPermissions.FORM_SUBMISSIONS, StatsPermissions.FORM_SUBMISSIONS_DISPLAY_NAME)]
[UIPermission(StatsPermissions.CONTENT_INVENTORY, StatsPermissions.CONTENT_INVENTORY_DISPLAY_NAME)]
[UIPermission(StatsPermissions.EVENT_LOG, StatsPermissions.EVENT_LOG_DISPLAY_NAME)]
[UIPermission(StatsPermissions.ORDERS_REVENUE, StatsPermissions.ORDERS_REVENUE_DISPLAY_NAME)]
public sealed class StatsApplicationPage : ApplicationPage
{
    public const string IDENTIFIER = "Kentico.Xperience.AdminStats.Application";
}
