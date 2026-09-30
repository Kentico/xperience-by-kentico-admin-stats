using System.Globalization;

namespace Kentico.Xperience.AdminStats.Shared;

/// <summary>
/// Admin paths of items in channel applications (pages, emails, headless items), relative to the admin root like
/// <see cref="StatsAdminLinks"/> paths (the client adds the admin prefix, see <c>adminLinks.ts</c>).
/// </summary>
/// <remarks>
/// These paths are built by hand, not with <c>IPageLinkGenerator</c>: the channel applications are dynamic applications
/// whose root slug is composed at run time per channel (for example <c>webpages-{websiteChannelId}</c>), and there is no
/// public API to pass that slug to <c>IPageLinkGenerator.GetPath</c>. The same formats are used by the Content Model Graph
/// Labs project (<c>ContentItemRelationshipGraphBuilder</c>). Keep every format here, so a product change is fixed in one place.
/// </remarks>
public static class StatsChannelItemPaths
{
    /// <summary>
    /// Content tab of a page: <c>/webpages-{websiteChannelId}/{languageName}_{webPageItemId}/content</c>.
    /// </summary>
    public const string WebPageFormat = "/webpages-{0}/{1}_{2}/content";

    /// <summary>
    /// Email in the email channel's list: <c>/emails-{emailChannelId}/{languageName}/list/{emailConfigurationId}</c>.
    /// </summary>
    public const string EmailFormat = "/emails-{0}/{1}/list/{2}";

    /// <summary>
    /// Headless item in the headless channel's list: <c>/headless-{headlessChannelId}/{languageName}/list/{headlessItemId}</c>.
    /// </summary>
    public const string HeadlessItemFormat = "/headless-{0}/{1}/list/{2}";

    /// <param name="websiteChannelId"><c>WebsiteChannelID</c> (not the <c>ChannelID</c>).</param>
    /// <param name="languageName">Content language code name.</param>
    /// <param name="webPageItemId"><c>WebPageItemID</c>.</param>
    public static string? GetWebPagePath(int websiteChannelId, string? languageName, int webPageItemId) =>
        Format(WebPageFormat, websiteChannelId, languageName, webPageItemId);

    /// <param name="emailChannelId"><c>EmailChannelID</c> (not the <c>ChannelID</c>).</param>
    /// <param name="languageName">Content language code name.</param>
    /// <param name="emailConfigurationId"><c>EmailConfigurationID</c>.</param>
    public static string? GetEmailPath(int emailChannelId, string? languageName, int emailConfigurationId) =>
        Format(EmailFormat, emailChannelId, languageName, emailConfigurationId);

    /// <param name="headlessChannelId"><c>HeadlessChannelID</c> (not the <c>ChannelID</c>).</param>
    /// <param name="languageName">Content language code name.</param>
    /// <param name="headlessItemId"><c>HeadlessItemID</c>.</param>
    public static string? GetHeadlessItemPath(int headlessChannelId, string? languageName, int headlessItemId) =>
        Format(HeadlessItemFormat, headlessChannelId, languageName, headlessItemId);

    /// <summary>
    /// Returns <c>null</c> for IDs &lt;= 0 or an empty language; the language is URL-encoded.
    /// </summary>
    private static string? Format(string format, int channelId, string? languageName, int itemId) =>
        channelId > 0 && itemId > 0 && !string.IsNullOrWhiteSpace(languageName)
            ? string.Format(CultureInfo.InvariantCulture, format, channelId, Uri.EscapeDataString(languageName.Trim()), itemId)
            : null;
}
