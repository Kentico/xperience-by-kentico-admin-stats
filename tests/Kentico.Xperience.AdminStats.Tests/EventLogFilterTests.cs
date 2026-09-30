using Kentico.Xperience.AdminStats.Reports.EventLog;
using Kentico.Xperience.AdminStats.Shared;

namespace Kentico.Xperience.AdminStats.Tests;

public class EventLogFilterTests
{
    private static readonly DateOnly today = new(2026, 9, 29);

    [Test]
    public void Normalize_Defaults_AllTypesLast30Days()
    {
        var query = new EventLogFilter().Normalize(today);

        Assert.That(query.EventType, Is.Null);
        Assert.That(query.Range, Is.EqualTo(new StatsFilter().Normalize(today)));
    }

    [TestCase("E", "E")]
    [TestCase("w", "W")]
    [TestCase(" i ", "I")]
    [TestCase("X", null)]
    [TestCase("", null)]
    [TestCase(null, null)]
    public void Normalize_EventType_KnownCodesOnly(string? value, string? expected)
    {
        var query = new EventLogFilter { EventType = value }.Normalize(today);

        Assert.That(query.EventType, Is.EqualTo(expected));
    }

    [Test]
    public void Normalize_DropsChannel_KeepsRangeAndGrouping()
    {
        var filter = new EventLogFilter
        {
            Range = new StatsFilter { From = new(2026, 9, 1), To = new(2026, 9, 10), Grouping = StatsGrouping.Week, ChannelId = 3 },
        };

        var query = filter.Normalize(today);

        Assert.That(query.Range, Is.EqualTo(new StatsQuery(new(2026, 9, 1), new(2026, 9, 10), StatsGrouping.Week, null)));
    }

    [Test]
    public void EventTypes_UseProductConstants_InformationFirst()
    {
        Assert.That(EventLogTypes.All.Select(t => t.Key), Is.EqualTo(new[]
        {
            CMS.EventLog.EventType.INFORMATION,
            CMS.EventLog.EventType.WARNING,
            CMS.EventLog.EventType.ERROR,
        }));
    }

    [Test]
    public void Sql_AddsTypeConditionOnlyWhenFiltering()
    {
        string all = EventLogSql.Build(filterByType: false, sourcePrefixCount: 2, sourceExactCount: 1);
        string filtered = EventLogSql.Build(filterByType: true, sourcePrefixCount: 2, sourceExactCount: 1);

        Assert.That(all, Does.Not.Contain(EventLogSql.EventTypeParameter));
        // Sources (all, Xperience, custom), event codes and users; the daily counts stay unfiltered.
        Assert.That(filtered.Split(EventLogSql.EventTypeParameter).Length - 1, Is.EqualTo(5));
        Assert.That(filtered.Split("SELECT").Length - 1, Is.EqualTo(all.Split("SELECT").Length - 1));
    }

    [Test]
    public void Sql_SplitsSourcesByPrefixParameters()
    {
        string sql = EventLogSql.Build(filterByType: false, sourcePrefixCount: 2, sourceExactCount: 1);

        const string xperience = @"([Source] LIKE @SourcePrefix0 ESCAPE N'\' OR [Source] LIKE @SourcePrefix1 ESCAPE N'\' OR [Source] = @SourceExact0)";
        Assert.That(sql, Does.Contain($"AND {xperience}"));
        Assert.That(sql, Does.Contain($"AND NOT {xperience}"));
        Assert.That(sql, Does.Not.Contain("@SourcePrefix2"));
        Assert.That(sql, Does.Not.Contain("@SourceExact1"));
    }

    [Test]
    public void Sql_WithoutPrefixes_AllSourcesAreCustom()
    {
        string sql = EventLogSql.Build(filterByType: false, sourcePrefixCount: 0, sourceExactCount: 0);

        Assert.That(sql, Does.Contain("AND 1 = 0"));
        Assert.That(sql, Does.Contain("AND NOT 1 = 0"));
        Assert.That(sql, Does.Not.Contain(EventLogSql.SourcePrefixParameter));
        Assert.That(sql, Does.Not.Contain(EventLogSql.SourceExactParameter));
    }
}
