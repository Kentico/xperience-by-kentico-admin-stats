using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class StatsComparisonTests
{
    private static StatsQuery Query(DateOnly from, DateOnly to) => new(from, to, StatsGrouping.Day, null);

    [Test]
    public void GetPreviousRange_SameLengthEndingDayBeforeFrom()
    {
        var (from, to) = StatsComparison.GetPreviousRange(Query(new(2026, 9, 1), new(2026, 9, 30)));

        Assert.That((from, to), Is.EqualTo((new DateOnly(2026, 8, 2), new DateOnly(2026, 8, 31))));
    }

    [Test]
    public void GetPreviousRange_OneDay_IsDayBefore()
    {
        var (from, to) = StatsComparison.GetPreviousRange(Query(new(2026, 3, 1), new(2026, 3, 1)));

        Assert.That((from, to), Is.EqualTo((new DateOnly(2026, 2, 28), new DateOnly(2026, 2, 28))));
    }

    [Test]
    public void GetPreviousRange_CrossesLeapDayAndYear()
    {
        // Mar 1–31, 2024 (31 days) -> Jan 30 – Feb 29, 2024.
        var (from, to) = StatsComparison.GetPreviousRange(Query(new(2024, 3, 1), new(2024, 3, 31)));
        Assert.That((from, to), Is.EqualTo((new DateOnly(2024, 1, 30), new DateOnly(2024, 2, 29))));

        // Jan 1–10, 2025 -> Dec 22–31, 2024.
        (from, to) = StatsComparison.GetPreviousRange(Query(new(2025, 1, 1), new(2025, 1, 10)));
        Assert.That((from, to), Is.EqualTo((new DateOnly(2024, 12, 22), new DateOnly(2024, 12, 31))));
    }

    [Test]
    public void GetPreviousRange_ClampsAtMinValue()
    {
        var (from, to) = StatsComparison.GetPreviousRange(Query(DateOnly.MinValue.AddDays(2), DateOnly.MinValue.AddDays(9)));

        Assert.That((from, to), Is.EqualTo((DateOnly.MinValue, DateOnly.MinValue.AddDays(1))));
    }

    [TestCase(112, 100, 0.12)]
    [TestCase(50, 100, -0.5)]
    [TestCase(0, 10, -1.0)]
    [TestCase(7, 7, 0.0)]
    public void Create_ChangeIsRatioOfPrevious(int current, int previous, double expected)
    {
        var comparison = StatsComparison.Create(Query(new(2026, 9, 1), new(2026, 9, 30)), current, previous);

        Assert.That(comparison.Change, Is.EqualTo(expected).Within(1e-9));
        Assert.That(comparison.Current, Is.EqualTo(current));
        Assert.That(comparison.Previous, Is.EqualTo(previous));
        Assert.That((comparison.PreviousFrom, comparison.PreviousTo), Is.EqualTo((new DateOnly(2026, 8, 2), new DateOnly(2026, 8, 31))));
    }

    [TestCase(0)]
    [TestCase(5)]
    public void Create_PreviousZero_ChangeIsNull(int current) =>
        Assert.That(StatsComparison.Create(Query(new(2026, 9, 1), new(2026, 9, 30)), current, 0).Change, Is.Null);
}
