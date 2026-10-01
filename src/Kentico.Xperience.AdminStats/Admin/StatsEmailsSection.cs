using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.AdminStats.Admin;

[assembly: UIPage(
    uiPageType: typeof(StatsEmailsSection),
    parentType: typeof(StatsApplicationPage),
    slug: "emails",
    name: "Emails",
    templateName: TemplateNames.SECTION_LAYOUT,
    order: 150,
    Icon = Icons.PaperPlane)]

namespace Kentico.Xperience.AdminStats.Admin;

/// <summary>
/// Email marketing reports: recipient lists.
/// </summary>
public sealed class StatsEmailsSection(
    IUIPermissionEvaluator permissionEvaluator,
    IPageLinkGenerator pageLinkGenerator) : StatsSectionPage(permissionEvaluator, pageLinkGenerator)
{
}
