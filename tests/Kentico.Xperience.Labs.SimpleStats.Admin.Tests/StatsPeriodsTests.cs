using System.Globalization;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class StatsPeriodsTests
{
    [TestCase("2026-09-29", StatsGrouping.Day, "2026-09-29")]
    [TestCase("2026-09-29", StatsGrouping.Week, "2026-09-28")] // Tuesday -> Monday
    [TestCase("2026-09-28", StatsGrouping.Week, "2026-09-28")] // Monday stays
    [TestCase("2026-10-04", StatsGrouping.Week, "2026-09-28")] // Sunday -> previous Monday
    [TestCase("2026-09-29", StatsGrouping.Month, "2026-09-01")]
    public void GetPeriodStart_ReturnsBucketStart(string date, StatsGrouping grouping, string expected) =>
        Assert.That(
            StatsPeriods.GetPeriodStart(DateOnly.Parse(date, CultureInfo.InvariantCulture), grouping),
            Is.EqualTo(DateOnly.Parse(expected, CultureInfo.InvariantCulture)));

    [Test]
    public void Build_Day_ReturnsOnePeriodPerDay()
    {
        var periods = StatsPeriods.Build(new(2026, 9, 1), new(2026, 9, 30), StatsGrouping.Day);

        Assert.That(periods, Has.Count.EqualTo(30));
        Assert.That(periods[0].Start, Is.EqualTo(new DateOnly(2026, 9, 1)));
        Assert.That(periods[^1].Start, Is.EqualTo(new DateOnly(2026, 9, 30)));
        Assert.That(periods[0].Label, Is.EqualTo("Sep 1"));
    }

    [Test]
    public void Build_Week_IncludesPartialWeeksAtBothEnds()
    {
        // Wed 2026-09-02 .. Tue 2026-09-15 -> weeks of Aug 31, Sep 7, Sep 14
        var periods = StatsPeriods.Build(new(2026, 9, 2), new(2026, 9, 15), StatsGrouping.Week);

        Assert.That(periods.Select(p => p.Start), Is.EqualTo(new[]
        {
            new DateOnly(2026, 8, 31),
            new DateOnly(2026, 9, 7),
            new DateOnly(2026, 9, 14),
        }));
    }

    [Test]
    public void Build_Month_AcrossYears_UsesMonthLabels()
    {
        var periods = StatsPeriods.Build(new(2025, 11, 15), new(2026, 2, 3), StatsGrouping.Month);

        Assert.That(periods.Select(p => p.Label), Is.EqualTo(new[] { "Nov 2025", "Dec 2025", "Jan 2026", "Feb 2026" }));
    }

    [Test]
    public void Build_Day_AcrossYears_IncludesYearInLabel()
    {
        var periods = StatsPeriods.Build(new(2025, 12, 31), new(2026, 1, 1), StatsGrouping.Day);

        Assert.That(periods.Select(p => p.Label), Is.EqualTo(new[] { "Dec 31, 2025", "Jan 1, 2026" }));
    }

    [Test]
    public void Build_ReversedRange_ReturnsEmpty() =>
        Assert.That(StatsPeriods.Build(new(2026, 9, 2), new(2026, 9, 1), StatsGrouping.Day), Is.Empty);
}
