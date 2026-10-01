using System.Collections.Concurrent;
using System.Reflection;

using Kentico.Xperience.Admin.Base;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// Hides the Stats (Labs) pages the current user may not open.
/// The product builds child routes and navigation items without evaluating permissions, and its default route
/// is the first child route, so a role with only some report permissions would see denied reports and land on a 403.
/// </summary>
internal static class StatsNavigation
{
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<UIPageAttribute>> childPages = new();

    /// <summary>
    /// Returns the pages registered under <paramref name="parentType"/> in its assembly, by order.
    /// </summary>
    public static IReadOnlyList<UIPageAttribute> GetChildPages(Type parentType) =>
        childPages.GetOrAdd(parentType, static type => type.Assembly
            .GetCustomAttributes<UIPageAttribute>()
            .Where(page => page.ParentType == type)
            .OrderBy(page => page.Order)
            .ToList());

    /// <summary>
    /// Returns the slugs of the child pages of <paramref name="parentType"/> the user may not open.
    /// A page is denied when its <see cref="UIEvaluatePermissionAttribute"/> permission is not granted.
    /// A section is denied when all its pages are denied.
    /// </summary>
    public static async Task<IReadOnlySet<string>> GetDeniedChildSlugs(Type parentType, Func<string, Task<bool>> isGranted)
    {
        var denied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var child in GetChildPages(parentType))
        {
            if (!await CanOpen(child.Type, isGranted))
            {
                denied.Add(child.Slug);
            }
        }

        return denied;
    }

    /// <summary>
    /// Returns the first route the user may open, or <c>null</c> when all are denied.
    /// </summary>
    public static Route? GetDefaultRoute(IEnumerable<Route> routes, IReadOnlySet<string> deniedSlugs) =>
        routes.FirstOrDefault(route => !deniedSlugs.Contains(route.Path));

    public static IEnumerable<NavigationItem> FilterNavigation(IEnumerable<NavigationItem> items, IReadOnlySet<string> deniedSlugs) =>
        items.Where(item => !deniedSlugs.Contains(item.Path)).ToList();

    private static async Task<bool> CanOpen(Type pageType, Func<string, Task<bool>> isGranted)
    {
        string? permission = pageType.GetCustomAttribute<UIEvaluatePermissionAttribute>(inherit: true)?.Permission;
        if (permission is not null && !await isGranted(permission))
        {
            return false;
        }

        if (!typeof(StatsSectionPage).IsAssignableFrom(pageType))
        {
            return true;
        }

        // A section only opens its first page, so it is useful only when the user may open one of its pages.
        foreach (var child in GetChildPages(pageType))
        {
            if (await CanOpen(child.Type, isGranted))
            {
                return true;
            }
        }

        return false;
    }
}
