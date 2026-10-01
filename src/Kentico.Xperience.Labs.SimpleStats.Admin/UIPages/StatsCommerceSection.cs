using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

[assembly: UIPage(
    uiPageType: typeof(StatsCommerceSection),
    parentType: typeof(StatsApplicationPage),
    slug: "commerce",
    name: "Commerce",
    templateName: TemplateNames.SECTION_LAYOUT,
    order: 300,
    Icon = Icons.ShoppingCart)]

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// Digital commerce reports: orders and revenue, and customers.
/// </summary>
public sealed class StatsCommerceSection(
    IUIPermissionEvaluator permissionEvaluator,
    IPageLinkGenerator pageLinkGenerator) : StatsSectionPage(permissionEvaluator, pageLinkGenerator)
{
}
