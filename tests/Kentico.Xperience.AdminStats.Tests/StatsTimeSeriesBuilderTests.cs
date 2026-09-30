using Kentico.Xperience.AdminStats.Shared;

namespace Kentico.Xperience.AdminStats.Tests;

public class StatsTimeSeriesBuilderTests
{
    private static readonly StatsSeriesDefinition[] definitions =
    [
        new("b", "Bravo"),
        new("a", "Alpha"),
    ];

    [Test]
    public void BuildFixed_KeepsDefinitionOrder_NotTotals()
    {
        var query = new StatsQuery(new(2026, 9, 1), new(2026, 9, 2), StatsGrouping.Day, null);
        StatsDailyCount[] rows =
        [
            new("a", new(2026, 9, 1), 10),
            new("b", new(2026, 9, 2), 1),
        ];

        var result = StatsTimeSeriesBuilder.BuildFixed(query, rows, definitions);

        Assert.That(result.Series.Select(s => s.Key), Is.EqualTo(new[] { "b", "a" }));
        Assert.That(result.Series.Select(s => s.DisplayName), Is.EqualTo(new[] { "Bravo", "Alpha" }));
        Assert.That(result.Series[0].Values, Is.EqualTo(new[] { 0, 1 }));
        Assert.That(result.Series[1].Values, Is.EqualTo(new[] { 10, 0 }));
        Assert.That(result.Total, Is.EqualTo(11));
    }

    [Test]
    public void BuildFixed_NoData_ReturnsEveryDefinedSeriesZeroFilled()
    {
        var query = new StatsQuery(new(2026, 9, 1), new(2026, 9, 3), StatsGrouping.Day, null);

        var result = StatsTimeSeriesBuilder.BuildFixed(query, [], definitions);

        Assert.Multiple(() =>
        {
            Assert.That(result.Periods, Has.Count.EqualTo(3));
            Assert.That(result.Series, Has.Count.EqualTo(2));
            Assert.That(result.Series, Has.All.Matches<StatsTimeSeries>(s => s.Values.Count == 3 && s.Values.All(v => v == 0) && s.Total == 0));
            Assert.That(result.Total, Is.Zero);
        });
    }

    [Test]
    public void BuildFixed_IgnoresUndefinedKeys_AndMatchesKeysIgnoringCase()
    {
        var query = new StatsQuery(new(2026, 9, 1), new(2026, 9, 1), StatsGrouping.Day, null);
        StatsDailyCount[] rows =
        [
            new("A", new(2026, 9, 1), 2),
            new("other", new(2026, 9, 1), 50),
        ];

        var result = StatsTimeSeriesBuilder.BuildFixed(query, rows, definitions);

        Assert.That(result.Series[1].Total, Is.EqualTo(2));
        Assert.That(result.Total, Is.EqualTo(2));
    }

    [TestCase(StatsGrouping.Day, new[] { 2, 3, 0, 0, 0, 0, 0, 5 })]
    [TestCase(StatsGrouping.Week, new[] { 5, 5 })]
    [TestCase(StatsGrouping.Month, new[] { 10 })]
    public void BuildFixed_BucketsPerGrouping(StatsGrouping grouping, int[] expected)
    {
        // Mon Sep 7 .. Mon Sep 14 -> two weeks, one month
        var query = new StatsQuery(new(2026, 9, 7), new(2026, 9, 14), grouping, null);
        StatsDailyCount[] rows =
        [
            new("a", new(2026, 9, 7), 2),
            new("a", new(2026, 9, 8), 3),
            new("a", new(2026, 9, 14), 5),
        ];

        var result = StatsTimeSeriesBuilder.BuildFixed(query, rows, definitions);

        Assert.That(result.Series[1].Values, Is.EqualTo(expected));
    }

    [Test]
    public void BuildFixed_IgnoresRowsOutsideRangeAndNonPositiveCounts()
    {
        var query = new StatsQuery(new(2026, 9, 1), new(2026, 9, 2), StatsGrouping.Week, 4);
        StatsDailyCount[] rows =
        [
            new("a", new(2026, 8, 31), 10), // same week as Sep 1, but before From
            new("a", new(2026, 9, 1), 1),
            new("a", new(2026, 9, 2), -3),
        ];

        var result = StatsTimeSeriesBuilder.BuildFixed(query, rows, definitions);

        Assert.That(result.Series[1].Values, Is.EqualTo(new[] { 1 }));
        Assert.That(result.ChannelId, Is.EqualTo(4));
    }

    [Test]
    public void BuildByTotal_SortsByTotalThenName_AndOnlyReturnsKeysWithData()
    {
        var query = new StatsQuery(new(2026, 9, 1), new(2026, 9, 2), StatsGrouping.Day, null);
        StatsDailyCount[] rows =
        [
            new("x", new(2026, 9, 1), 1),
            new("y", new(2026, 9, 1), 7),
            new("z", new(2026, 9, 2), 1),
        ];

        var result = StatsTimeSeriesBuilder.BuildByTotal(query, rows, key => key.ToUpperInvariant());

        Assert.That(result.Series.Select(s => s.DisplayName), Is.EqualTo(new[] { "Y", "X", "Z" }));
        Assert.That(result.Total, Is.EqualTo(9));
    }
}
