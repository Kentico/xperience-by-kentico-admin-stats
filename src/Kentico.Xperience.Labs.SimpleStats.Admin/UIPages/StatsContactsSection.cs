using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

[assembly: UIPage(
    uiPageType: typeof(StatsContactsSection),
    parentType: typeof(StatsApplicationPage),
    slug: "contacts",
    name: "Contacts",
    templateName: TemplateNames.SECTION_LAYOUT,
    order: 100,
    Icon = Icons.Users)]

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// Contact and activity reports: activity counts, top pages, new contacts, form submissions, member registrations and consents.
/// </summary>
public sealed class StatsContactsSection(
    IUIPermissionEvaluator permissionEvaluator,
    IPageLinkGenerator pageLinkGenerator) : StatsSectionPage(permissionEvaluator, pageLinkGenerator)
{
}
