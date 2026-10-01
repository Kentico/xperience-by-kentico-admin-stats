using Kentico.Xperience.Admin.Base;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// Section of the "Simple Stats (Labs)" application that groups reports. Shown in the application navigation,
/// opens its first report the user may open, and hides the reports the user has no permission for.
/// Sections have no permission of their own. A section without an allowed report redirects to <see cref="StatsNoReportsPage"/>.
/// </summary>
[UINavigation(true)]
public abstract class StatsSectionPage(
    IUIPermissionEvaluator permissionEvaluator,
    IPageLinkGenerator pageLinkGenerator) : SecondaryMenuSectionPage
{
    private readonly IUIPermissionEvaluator permissionEvaluator = permissionEvaluator;
    private readonly IPageLinkGenerator pageLinkGenerator = pageLinkGenerator;

    private IReadOnlySet<string> deniedSlugs = new HashSet<string>();

    public override async Task ConfigurePage()
    {
        await base.ConfigurePage();

        // Runs before the product builds routes and navigation, which are synchronous.
        deniedSlugs = await StatsNavigation.GetDeniedChildSlugs(
            GetType(),
            async permission => (await permissionEvaluator.Evaluate(permission)).Succeeded);
    }

    public override Task<TemplateClientProperties> ConfigureTemplateProperties(TemplateClientProperties properties)
    {
        properties.Navigation.Items = StatsNavigation.FilterNavigation(properties.Navigation.Items, deniedSlugs);

        if (properties.DefaultRoute is null)
        {
            // Only reachable by URL: the application hides sections without an allowed report.
            properties.RedirectUrl = pageLinkGenerator.GetPath<StatsNoReportsPage>();
        }

        return base.ConfigureTemplateProperties(properties);
    }

    protected override Route GetDefaultRoute(IEnumerable<Route> routes) =>
        StatsNavigation.GetDefaultRoute(routes, deniedSlugs)!;
}
