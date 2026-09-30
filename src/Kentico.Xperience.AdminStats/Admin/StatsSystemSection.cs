using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.AdminStats.Admin;

[assembly: UIPage(
    uiPageType: typeof(StatsSystemSection),
    parentType: typeof(StatsApplicationPage),
    slug: "system",
    name: "System",
    templateName: TemplateNames.SECTION_LAYOUT,
    order: 400,
    Icon = Icons.Cogwheels)]

namespace Kentico.Xperience.AdminStats.Admin;

/// <summary>
/// System reports: event log.
/// </summary>
public sealed class StatsSystemSection(
    IUIPermissionEvaluator permissionEvaluator,
    IPageLinkGenerator pageLinkGenerator) : StatsSectionPage(permissionEvaluator, pageLinkGenerator)
{
}
