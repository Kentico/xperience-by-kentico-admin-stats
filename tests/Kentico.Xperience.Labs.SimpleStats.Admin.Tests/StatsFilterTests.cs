using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class StatsFilterTests
{
    private static readonly DateOnly today = new(2026, 9, 29);

    [Test]
    public void Normalize_Empty_UsesLast30DaysByDay()
    {
        var query = new StatsFilter().Normalize(today);

        Assert.That(query, Is.EqualTo(new StatsQuery(new(2026, 8, 31), today, StatsGrouping.Day, null)));
    }

    [Test]
    public void Normalize_ReversedRange_Swaps()
    {
        var query = new StatsFilter { From = new(2026, 9, 10), To = new(2026, 9, 1) }.Normalize(today);

        Assert.Multiple(() =>
        {
            Assert.That(query.From, Is.EqualTo(new DateOnly(2026, 9, 1)));
            Assert.That(query.To, Is.EqualTo(new DateOnly(2026, 9, 10)));
        });
    }

    [Test]
    public void Normalize_TooLongRange_IsShortenedFromStart()
    {
        var query = new StatsFilter { From = new(2000, 1, 1), To = today }.Normalize(today);

        Assert.That(query.To.DayNumber - query.From.DayNumber + 1, Is.EqualTo(StatsFilter.MaxRangeDays));
        Assert.That(query.To, Is.EqualTo(today));
    }

    [TestCase(0)]
    [TestCase(-5)]
    public void Normalize_NonPositiveChannel_MeansAllChannels(int channelId) =>
        Assert.That(new StatsFilter { ChannelId = channelId }.Normalize(today).ChannelId, Is.Null);

    [Test]
    public void Normalize_UndefinedGrouping_FallsBackToDay() =>
        Assert.That(new StatsFilter { Grouping = (StatsGrouping)42 }.Normalize(today).Grouping, Is.EqualTo(StatsGrouping.Day));
}
