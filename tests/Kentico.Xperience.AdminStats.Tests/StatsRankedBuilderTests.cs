using Kentico.Xperience.AdminStats.Shared;

namespace Kentico.Xperience.AdminStats.Tests;

public class StatsRankedBuilderTests
{
    private static readonly StatsQuery query = new(new(2026, 9, 1), new(2026, 9, 30), StatsGrouping.Day, 2);

    [Test]
    public void Build_Empty_ReturnsNoItemsAndZeroTotal()
    {
        var result = StatsRankedBuilder.Build(query, [], total: 0, itemCount: 0, limit: 25);

        Assert.That(result.Items, Is.Empty);
        Assert.That(result.Total, Is.Zero);
        Assert.That(result.ItemCount, Is.Zero);
        Assert.That(result.From, Is.EqualTo(query.From));
        Assert.That(result.To, Is.EqualTo(query.To));
        Assert.That(result.ChannelId, Is.EqualTo(2));
    }

    [Test]
    public void Build_ComputesShareFromTotal()
    {
        // Total includes items outside the top N.
        var result = StatsRankedBuilder.Build(query, [Entry("a", 30), Entry("b", 10)], total: 100, itemCount: 5, limit: 25);

        Assert.That(result.Total, Is.EqualTo(100));
        Assert.That(result.ItemCount, Is.EqualTo(5));
        Assert.That(result.Items.Select(i => i.Share), Is.EqualTo(new[] { 0.3, 0.1 }).Within(1e-9));
    }

    [Test]
    public void Build_OrdersByValueThenKey_AndRanksFromOne()
    {
        var result = StatsRankedBuilder.Build(
            query,
            [Entry("c", 5), Entry("b", 9), Entry("a", 5)],
            total: 19,
            itemCount: 3,
            limit: 25);

        Assert.That(result.Items.Select(i => i.Key), Is.EqualTo(new[] { "b", "a", "c" }));
        Assert.That(result.Items.Select(i => i.Rank), Is.EqualTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public void Build_AppliesLimit()
    {
        var result = StatsRankedBuilder.Build(query, [Entry("a", 3), Entry("b", 2), Entry("c", 1)], total: 6, itemCount: 3, limit: 2);

        Assert.That(result.Items.Select(i => i.Key), Is.EqualTo(new[] { "a", "b" }));
        Assert.That(result.Total, Is.EqualTo(6));
        Assert.That(result.ItemCount, Is.EqualTo(3));
    }

    [Test]
    public void Build_DropsZeroValuesAndDuplicateKeys()
    {
        var result = StatsRankedBuilder.Build(query, [Entry("a", 4), Entry("a", 9), Entry("b", 0)], total: 4, itemCount: 1, limit: 25);

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].Value, Is.EqualTo(4));
        Assert.That(result.Items[0].Share, Is.EqualTo(1));
    }

    [Test]
    public void Build_RaisesTotalAndCount_WhenLowerThanEntries()
    {
        var result = StatsRankedBuilder.Build(query, [Entry("a", 4), Entry("b", 6)], total: 0, itemCount: 0, limit: 25);

        Assert.That(result.Total, Is.EqualTo(10));
        Assert.That(result.ItemCount, Is.EqualTo(2));
        Assert.That(result.Items[0].Share, Is.EqualTo(0.6).Within(1e-9));
    }

    private static StatsRankedEntry Entry(string key, int value) => new(key, key, null, value, null, null);
}
