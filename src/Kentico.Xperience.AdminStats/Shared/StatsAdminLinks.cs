using Kentico.Xperience.Admin.Base;

namespace Kentico.Xperience.AdminStats.Shared;

/// <summary>
/// Builds paths to native admin pages with the product's <see cref="IPageLinkGenerator"/>, so no admin URL is hardcoded.
/// Paths are relative to the admin root (for example <c>/forms/list/1/submissions</c>, without the admin path prefix).
/// The client turns them into links by replacing the current report page path in the browser location
/// (see <c>adminLinks.ts</c>), because the admin path prefix has no public server API.
/// </summary>
internal interface IStatsAdminLinks
{
    /// <summary>
    /// Returns the path of the page, or <c>null</c> when the page is not registered or a parameter is missing
    /// (for example when an admin application is not installed).
    /// </summary>
    public string? GetPath<TPage>(PageParameterValues? parameters = null);
}

internal sealed class StatsAdminLinks(IPageLinkGenerator pageLinkGenerator) : IStatsAdminLinks
{
    private readonly IPageLinkGenerator pageLinkGenerator = pageLinkGenerator;

    public string? GetPath<TPage>(PageParameterValues? parameters = null)
    {
        try
        {
            return pageLinkGenerator.GetPath<TPage>(parameters);
        }
        catch (InvalidOperationException)
        {
            // Page not in the UI tree or parameters do not match. The report works without links.
            return null;
        }
    }
}
