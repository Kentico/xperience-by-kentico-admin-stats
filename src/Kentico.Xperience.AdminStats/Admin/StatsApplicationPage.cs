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
/// Assign the View permission to roles that may see the reports.
/// </summary>
[UIPermission(SystemPermissions.VIEW)]
public sealed class StatsApplicationPage : ApplicationPage
{
    public const string IDENTIFIER = "Kentico.Xperience.AdminStats.Application";
}
