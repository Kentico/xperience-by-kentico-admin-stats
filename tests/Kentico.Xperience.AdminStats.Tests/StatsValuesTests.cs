using System.Text.Json;

using Kentico.Xperience.AdminStats.Shared;

namespace Kentico.Xperience.AdminStats.Tests;

public class StatsValuesTests
{
    private static readonly StatsQuery range = new(new(2026, 9, 1), new(2026, 9, 30), StatsGrouping.Day, null);

    [TestCase(2.345, 2.35)]
    [TestCase(2.344, 2.34)]
    [TestCase(2.005, 2.01)]
    [TestCase(-2.005, -2.01)]
    [TestCase(10, 10)]
    public void Round_Amount_TwoDecimalsAwayFromZero(decimal value, decimal expected) =>
        Assert.That(StatsValues.Round(value, StatsValueKind.Amount), Is.EqualTo(expected));

    [TestCase(StatsValueKind.Count)]
    [TestCase(StatsValueKind.Ratio)]
    public void Round_CountAndRatio_Unchanged(StatsValueKind kind) =>
        Assert.That(StatsValues.Round(1.23456m, kind), Is.EqualTo(1.23456m));

    [Test]
    public void Round_Null_StaysNull() =>
        Assert.That(StatsValues.Round((decimal?)null, StatsValueKind.Amount), Is.Null);

    [Test]
    public void Divide_ByZero_IsNull()
    {
        Assert.That(StatsValues.Divide(10, 0), Is.Null);
        Assert.That(StatsValues.Divide(10, 4), Is.EqualTo(2.5m));
    }

    [Test]
    public void Comparison_RoundsValues_AndComputesChangeFromRoundedValues()
    {
        var comparison = StatsValueComparison.Create(range, StatsValueKind.Amount, 150.004m, 99.996m);

        Assert.That(comparison.Kind, Is.EqualTo(StatsValueKind.Amount));
        Assert.That(comparison.Current, Is.EqualTo(150.00m));
        Assert.That(comparison.Previous, Is.EqualTo(100.00m));
        Assert.That(comparison.Change, Is.EqualTo(0.5).Within(1e-9));
        Assert.That((comparison.PreviousFrom, comparison.PreviousTo), Is.EqualTo((new DateOnly(2026, 8, 2), new DateOnly(2026, 8, 31))));
    }

    [Test]
    public void Comparison_PreviousZero_ChangeNull()
    {
        var comparison = StatsValueComparison.Create(range, StatsValueKind.Count, 5, 0);

        Assert.That(comparison.Previous, Is.Zero);
        Assert.That(comparison.Change, Is.Null);
    }

    [TestCase(null, 10.0)]
    [TestCase(10.0, null)]
    [TestCase(null, null)]
    public void Comparison_MissingValue_ChangeNull(double? current, double? previous)
    {
        var comparison = StatsValueComparison.Create(range, StatsValueKind.Amount, (decimal?)current, (decimal?)previous);

        Assert.That(comparison.Current, Is.EqualTo((decimal?)current));
        Assert.That(comparison.Previous, Is.EqualTo((decimal?)previous));
        Assert.That(comparison.Change, Is.Null);
    }

    [Test]
    public void Kind_SerializesAsString() =>
        Assert.That(JsonSerializer.Serialize(StatsValueKind.Amount), Is.EqualTo("\"Amount\""));

    [Test]
    public void BuildValueSeries_BucketsZeroFillsAndRounds()
    {
        var weekly = range with { Grouping = StatsGrouping.Week };
        StatsDailyValue[] rows =
        [
            new("revenue", new(2026, 9, 1), 10.004m),
            new("revenue", new(2026, 9, 2), 0.004m),
            new("REVENUE", new(2026, 9, 8), 5m),
            new("orders", new(2026, 9, 8), 7m),
            new("revenue", new(2026, 8, 31), 100m),
            new("revenue", new(2026, 9, 9), 0m),
        ];

        var series = StatsTimeSeriesBuilder.BuildValueSeries(weekly, rows, new("revenue", "Revenue"), StatsValueKind.Amount);

        Assert.That(series.Key, Is.EqualTo("revenue"));
        Assert.That(series.DisplayName, Is.EqualTo("Revenue"));
        Assert.That(series.Kind, Is.EqualTo(StatsValueKind.Amount));
        // Weeks start on Monday: Aug 31, Sep 7, 14, 21, 28. Aug 31 itself is before the range.
        Assert.That(series.Values, Is.EqualTo(new[] { 10.01m, 5m, 0m, 0m, 0m }));
        Assert.That(series.Total, Is.EqualTo(15.01m));
    }

    [Test]
    public void BuildValueSeries_NoData_ZeroFilled()
    {
        var series = StatsTimeSeriesBuilder.BuildValueSeries(range, [], new("orders", "Orders"), StatsValueKind.Count);

        Assert.That(series.Values, Has.Count.EqualTo(30));
        Assert.That(series.Values, Is.All.Zero);
        Assert.That(series.Total, Is.Zero);
    }

    [Test]
    public void RankedResult_LeavesOutValueKinds_FromJson_WhenNull()
    {
        var result = StatsRankedBuilder.Build(range, [new("a", "A", null, 3, null, null)], 3, 1, 10);

        string json = JsonSerializer.Serialize(result);

        Assert.That(json, Does.Not.Contain("ValueKind"));
        Assert.That(json, Does.Contain("\"Value\":3,"));
        Assert.That(json, Does.Contain("\"Total\":3,"));
        Assert.That(
            JsonSerializer.Serialize(result with { ValueKind = StatsValueKind.Amount, SecondaryValueKind = StatsValueKind.Count }),
            Does.Contain("\"ValueKind\":\"Amount\"").And.Contain("\"SecondaryValueKind\":\"Count\""));
    }

    [Test]
    public void RankedBuilder_DecimalValues_ShareAndChange()
    {
        var result = StatsRankedBuilder.Build(
            range,
            [
                new StatsRankedEntry("a", "A", null, 75.50m, 2.5m, null) { PreviousValue = 50.25m },
                new StatsRankedEntry("b", "B", null, 24.50m, 1m, null) { PreviousValue = 0m },
            ],
            total: 100m,
            itemCount: 2,
            limit: 10);

        Assert.That(result.Items.Select(i => i.Value), Is.EqualTo(new[] { 75.50m, 24.50m }));
        Assert.That(result.Items[0].Share, Is.EqualTo(0.755).Within(1e-9));
        Assert.That(result.Items[0].SecondaryValue, Is.EqualTo(2.5m));
        Assert.That(result.Items[0].Change, Is.EqualTo((75.50 - 50.25) / 50.25).Within(1e-9));
        Assert.That(result.Items[1].Change, Is.Null);
    }

    [Test]
    public void RankedBuilder_KeepOrder_WithRange()
    {
        var result = StatsRankedBuilder.Build(
            range,
            [new("a", "A", null, 1, null, null), new("b", "B", null, 5, null, null)],
            total: 0,
            itemCount: 0,
            limit: 10,
            keepOrder: true);

        Assert.That(result.Items.Select(i => i.Key), Is.EqualTo(new[] { "a", "b" }));
        Assert.That(result.Total, Is.EqualTo(6));
        Assert.That(result.From, Is.EqualTo(range.From));
    }
}
