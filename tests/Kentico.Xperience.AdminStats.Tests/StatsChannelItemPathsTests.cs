using Kentico.Xperience.AdminStats.Shared;

namespace Kentico.Xperience.AdminStats.Tests;

public class StatsChannelItemPathsTests
{
    [Test]
    public void GetWebPagePath_UsesWebsiteChannelLanguageAndPage() =>
        Assert.That(StatsChannelItemPaths.GetWebPagePath(1, "en", 42), Is.EqualTo("/webpages-1/en_42/content"));

    [Test]
    public void GetEmailPath_UsesEmailChannelLanguageAndConfiguration() =>
        Assert.That(StatsChannelItemPaths.GetEmailPath(2, "es", 7), Is.EqualTo("/emails-2/es/list/7"));

    [Test]
    public void GetHeadlessItemPath_UsesHeadlessChannelLanguageAndItem() =>
        Assert.That(StatsChannelItemPaths.GetHeadlessItemPath(3, "en-US", 9), Is.EqualTo("/headless-3/en-US/list/9"));

    [Test]
    public void Paths_AreRelativeToAdminRoot()
    {
        // The client adds the admin prefix (adminLinks.ts), so no path contains it.
        string?[] paths =
        [
            StatsChannelItemPaths.GetWebPagePath(1, "en", 1),
            StatsChannelItemPaths.GetEmailPath(1, "en", 1),
            StatsChannelItemPaths.GetHeadlessItemPath(1, "en", 1),
        ];

        Assert.That(paths, Is.All.StartWith("/").And.All.Not.StartWith("/admin"));
    }

    [TestCase(0, "en", 1)]
    [TestCase(1, "en", 0)]
    [TestCase(1, "", 1)]
    [TestCase(1, null, 1)]
    public void GetWebPagePath_MissingValues_ReturnsNull(int channelId, string? language, int pageId) =>
        Assert.That(StatsChannelItemPaths.GetWebPagePath(channelId, language, pageId), Is.Null);

    [Test]
    public void GetWebPagePath_EncodesLanguage() =>
        Assert.That(StatsChannelItemPaths.GetWebPagePath(1, "a/b", 2), Is.EqualTo("/webpages-1/a%2Fb_2/content"));
}
