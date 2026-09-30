using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.AdminStats.Admin;

[assembly: UIPage(
    uiPageType: typeof(StatsContentSection),
    parentType: typeof(StatsApplicationPage),
    slug: "content",
    name: "Content",
    templateName: TemplateNames.SECTION_LAYOUT,
    order: 200,
    Icon = Icons.TreeStructure)]

namespace Kentico.Xperience.AdminStats.Admin;

/// <summary>
/// Content reports: content inventory.
/// </summary>
public sealed class StatsContentSection(
    IUIPermissionEvaluator permissionEvaluator,
    IPageLinkGenerator pageLinkGenerator) : StatsSectionPage(permissionEvaluator, pageLinkGenerator)
{
}
