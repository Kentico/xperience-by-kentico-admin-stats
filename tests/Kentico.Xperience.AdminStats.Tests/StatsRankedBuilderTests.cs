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

    [Test]
    public void Build_IncludeZero_KeepsZeroValuesLast_AndDropsNegative()
    {
        var result = StatsRankedBuilder.Build(
            query,
            [Entry("b", 0), Entry("a", 3), Entry("c", -1), Entry("d", 0)],
            total: 3,
            itemCount: 3,
            limit: 25,
            includeZero: true);

        Assert.That(result.Items.Select(i => i.Key), Is.EqualTo(new[] { "a", "b", "d" }));
        Assert.That(result.Items.Select(i => i.Rank), Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(result.Items[1].Share, Is.Zero);
    }

    [Test]
    public void Build_IncludeZero_AllZero_HasZeroShares()
    {
        var result = StatsRankedBuilder.Build(query, [Entry("a", 0)], total: 0, itemCount: 1, limit: 25, includeZero: true);

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Total, Is.Zero);
        Assert.That(result.Items[0].Share, Is.Zero);
    }

    [Test]
    public void Build_CopiesAdminPath()
    {
        var result = StatsRankedBuilder.Build(query, [Entry("a", 1) with { AdminPath = "/x/1" }, Entry("b", 1)], total: 2, itemCount: 2, limit: 25);

        Assert.That(result.Items.Select(i => i.AdminPath), Is.EqualTo(new[] { "/x/1", null }));
    }

    [Test]
    public void BuildSnapshot_HasNoRange_AndSortsByValue()
    {
        var result = StatsRankedBuilder.BuildSnapshot(2, [Entry("a", 1), Entry("b", 3)], total: 4, itemCount: 2, limit: 25);

        Assert.That(result.From, Is.EqualTo(DateOnly.MinValue));
        Assert.That(result.To, Is.EqualTo(DateOnly.MinValue));
        Assert.That(result.ChannelId, Is.EqualTo(2));
        Assert.That(result.Items.Select(i => i.Key), Is.EqualTo(new[] { "b", "a" }));
    }

    [Test]
    public void BuildSnapshot_KeepOrder_KeepsEntryOrderAndRanksByPosition()
    {
        var result = StatsRankedBuilder.BuildSnapshot(
            null,
            [Entry("published", 5), Entry("draft", 0), Entry("workflow", 7)],
            total: 12,
            itemCount: 3,
            limit: 25,
            includeZero: true,
            keepOrder: true);

        Assert.That(result.Items.Select(i => i.Key), Is.EqualTo(new[] { "published", "draft", "workflow" }));
        Assert.That(result.Items.Select(i => i.Rank), Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(result.Items[2].Share, Is.EqualTo(7d / 12).Within(1e-9));
    }

    private static StatsRankedEntry Entry(string key, int value) => new(key, key, null, value, null, null);
}
