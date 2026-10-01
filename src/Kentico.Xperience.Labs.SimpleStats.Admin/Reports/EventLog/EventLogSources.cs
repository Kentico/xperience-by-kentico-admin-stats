namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EventLog;

/// <summary>
/// Splits event sources into Xperience sources (logged by the product) and custom sources (everything else, usually project code).
/// </summary>
/// <remarks>
/// A source is an Xperience source when it starts with one of <see cref="XperiencePrefixes"/> or equals one of <see cref="XperienceNames"/>
/// (case-insensitive, like the default SQL collation).
/// Project code rarely logs under these namespaces, so everything else counts as custom.
/// </remarks>
public static class EventLogSources
{
    /// <summary>
    /// Escape character of the <c>LIKE</c> patterns from <see cref="ToLikePattern"/>.
    /// </summary>
    public const char LikeEscape = '\\';

    /// <summary>
    /// Source prefixes of Xperience events: the product namespaces <c>CMS.</c> and <c>Kentico.</c> (any <c>Kentico.*</c>,
    /// for example <c>Kentico.Xperience.*</c>, <c>Kentico.Web.Mvc.*</c>, <c>Kentico.Membership.*</c>).
    /// Other sources (for example <c>Microsoft.*</c> from ASP.NET Core) count as custom.
    /// </summary>
    public static IReadOnlyList<string> XperiencePrefixes { get; } = ["CMS.", "Kentico."];

    /// <summary>
    /// Exact source names of Xperience events without a product namespace (the web farm monitor).
    /// </summary>
    public static IReadOnlyList<string> XperienceNames { get; } = ["WebFarmMonitor"];

    /// <summary>
    /// Returns whether the source starts with one of <see cref="XperiencePrefixes"/> or equals one of <see cref="XperienceNames"/>.
    /// </summary>
    public static bool IsXperience(string? source) =>
        source is not null
        && (XperiencePrefixes.Any(prefix => source.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            || XperienceNames.Any(name => string.Equals(source, name, StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// Returns a <c>LIKE</c> pattern (with <see cref="LikeEscape"/>) that matches values starting with <paramref name="prefix"/>.
    /// The wildcards <c>%</c>, <c>_</c>, <c>[</c> and the escape character itself are escaped.
    /// </summary>
    public static string ToLikePattern(string prefix)
    {
        var pattern = new System.Text.StringBuilder(prefix.Length + 2);
        foreach (char c in prefix)
        {
            if (c is LikeEscape or '%' or '_' or '[')
            {
                pattern.Append(LikeEscape);
            }
            pattern.Append(c);
        }

        return pattern.Append('%').ToString();
    }
}
