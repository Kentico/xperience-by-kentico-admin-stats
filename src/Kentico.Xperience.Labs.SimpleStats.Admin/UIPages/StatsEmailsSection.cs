using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

[assembly: UIPage(
    uiPageType: typeof(StatsEmailsSection),
    parentType: typeof(StatsApplicationPage),
    slug: "emails",
    name: "Emails",
    templateName: TemplateNames.SECTION_LAYOUT,
    order: 150,
    Icon = Icons.PaperPlane)]

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// Email marketing reports: recipient lists.
/// </summary>
public sealed class StatsEmailsSection(
    IUIPermissionEvaluator permissionEvaluator,
    IPageLinkGenerator pageLinkGenerator) : StatsSectionPage(permissionEvaluator, pageLinkGenerator)
{
}
