using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

[assembly: UIPage(
    uiPageType: typeof(StatsSystemSection),
    parentType: typeof(StatsApplicationPage),
    slug: "system",
    name: "System",
    templateName: TemplateNames.SECTION_LAYOUT,
    order: 400,
    Icon = Icons.Cogwheels)]

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// System reports: event log.
/// </summary>
public sealed class StatsSystemSection(
    IUIPermissionEvaluator permissionEvaluator,
    IPageLinkGenerator pageLinkGenerator) : StatsSectionPage(permissionEvaluator, pageLinkGenerator)
{
}
