using Kentico.Xperience.AdminStats.Reports.TopPages;
using Kentico.Xperience.AdminStats.Shared;

namespace Kentico.Xperience.AdminStats.Tests;

public class TopPagesReportBuilderTests
{
    private static readonly StatsQuery query = new(new(2026, 9, 1), new(2026, 9, 30), StatsGrouping.Week, null);

    [Test]
    public void Build_MapsRowsToRankedItems()
    {
        var data = new TopPagesData(
            [
                new("https://example.com/", 60, 40, "Page visit 'Home'"),
                new("https://example.com/about", 20, 15, null),
            ],
            TotalVisits: 100,
            PageCount: 7);

        var result = TopPagesReportBuilder.Build(query, data, limit: 25);

        Assert.That(result.Total, Is.EqualTo(100));
        Assert.That(result.ItemCount, Is.EqualTo(7));

        var first = result.Items[0];
        Assert.That(first.Key, Is.EqualTo("https://example.com/"));
        Assert.That(first.Label, Is.EqualTo("https://example.com/"));
        Assert.That(first.SecondaryLabel, Is.EqualTo("Page visit 'Home'"));
        Assert.That(first.Value, Is.EqualTo(60));
        Assert.That(first.SecondaryValue, Is.EqualTo(40));
        Assert.That(first.Share, Is.EqualTo(0.6).Within(1e-9));
        Assert.That(first.Url, Is.EqualTo("https://example.com/"));

        Assert.That(result.Items[1].SecondaryLabel, Is.Null);
    }

    [Test]
    public void Build_EmptyUrl_UsesPlaceholderLabelAndNoLink()
    {
        var data = new TopPagesData([new(string.Empty, 3, 2, null)], 3, 1);

        var item = TopPagesReportBuilder.Build(query, data, limit: 25).Items.Single();

        Assert.That(item.Label, Is.EqualTo(TopPagesReportBuilder.NoUrlLabel));
        Assert.That(item.Url, Is.Null);
    }

    [Test]
    public void Build_Empty_ReturnsEmptyResult()
    {
        var result = TopPagesReportBuilder.Build(query, TopPagesData.Empty, limit: 25);

        Assert.That(result.Items, Is.Empty);
        Assert.That(result.Total, Is.Zero);
        Assert.That(result.ItemCount, Is.Zero);
    }

    [TestCase("https://example.com/a", true)]
    [TestCase("http://localhost:5000/", true)]
    [TestCase("/relative/path", false)]
    [TestCase("javascript:alert(1)", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    public void GetPublicUrl_OnlyAllowsAbsoluteHttpUrls(string? url, bool expected) =>
        Assert.That(TopPagesReportBuilder.GetPublicUrl(url) is not null, Is.EqualTo(expected));
}
