using Kentico.Xperience.AdminStats.Reports.EventLog;
using Kentico.Xperience.AdminStats.Shared;

namespace Kentico.Xperience.AdminStats.Tests;

public class EventLogReportBuilderTests
{
    // Sep 1–3; previous period Aug 29–31.
    private static readonly StatsQuery range = new(new(2026, 9, 1), new(2026, 9, 3), StatsGrouping.Day, null);
    private static readonly EventLogQuery allTypes = new(range, null);

    [Test]
    public void Build_EmptyLog_ReturnsAllTypesWithZeros()
    {
        var result = EventLogReportBuilder.Build(allTypes, EventLogReportData.Empty, 10000, _ => null);

        Assert.That(result.Trend.Series.Select(s => s.Key), Is.EqualTo(new[] { "I", "W", "E" }));
        Assert.That(result.Trend.Periods, Has.Count.EqualTo(3));
        Assert.That(result.Trend.Total, Is.Zero);
        Assert.That(result.Totals.Select(t => t.EventType), Is.EqualTo(new[] { "I", "W", "E" }));
        Assert.That(result.Totals.All(t => t.Comparison.Current == 0 && t.Comparison.Change is null));
        Assert.That(result.TotalComparison.Current, Is.Zero);
        Assert.That(result.TopSources.Items, Is.Empty);
        Assert.That(result.TopCodes.Items, Is.Empty);
        Assert.That(result.TopUsers.Items, Is.Empty);
        Assert.That(result.LogSizeLimit, Is.EqualTo(10000));
    }

    [Test]
    public void Build_SplitsPreviousPeriod_AndComparesPerType()
    {
        EventLogDailyCount[] daily =
        [
            new("E", new(2026, 8, 30), 2),
            new("E", new(2026, 9, 2), 3),
            new("I", new(2026, 9, 1), 10),
            new("W", new(2026, 8, 31), 4),
            new("I", new(2026, 8, 28), 99), // Before the previous period: ignored.
        ];

        var result = EventLogReportBuilder.Build(allTypes, Data(daily), null, _ => null);

        var errors = result.Totals.Single(t => t.EventType == "E").Comparison;
        Assert.That((errors.Current, errors.Previous), Is.EqualTo((3, 2)));
        Assert.That(errors.Change, Is.EqualTo(0.5).Within(1e-9));
        Assert.That((errors.PreviousFrom, errors.PreviousTo), Is.EqualTo((new DateOnly(2026, 8, 29), new DateOnly(2026, 8, 31))));

        var warnings = result.Totals.Single(t => t.EventType == "W").Comparison;
        Assert.That((warnings.Current, warnings.Previous, warnings.Change), Is.EqualTo((0, 4, -1.0)));

        var information = result.Totals.Single(t => t.EventType == "I").Comparison;
        Assert.That((information.Current, information.Previous, information.Change), Is.EqualTo((10, 0, (double?)null)));

        Assert.That((result.TotalComparison.Current, result.TotalComparison.Previous), Is.EqualTo((13, 6)));
        Assert.That(result.Trend.Series.Single(s => s.Key == "E").Values, Is.EqualTo(new[] { 0, 3, 0 }));
        Assert.That(result.Trend.Total, Is.EqualTo(13));
    }

    [Test]
    public void Build_TypeFilter_KeepsOnlyThatSeries_ButAllTotals()
    {
        EventLogDailyCount[] daily = [new("E", new(2026, 9, 2), 3), new("I", new(2026, 9, 1), 10)];
        var sources = new EventLogTop<EventLogGroupRow>([new("Smtp", 3, 1)], 1);

        var result = EventLogReportBuilder.Build(new(range, "E"), Data(daily) with { Sources = sources }, null, _ => null);

        Assert.That(result.EventType, Is.EqualTo("E"));
        Assert.That(result.Trend.Series.Select(s => s.Key), Is.EqualTo(new[] { "E" }));
        Assert.That(result.Trend.Total, Is.EqualTo(3));
        Assert.That(result.Totals, Has.Count.EqualTo(3));
        Assert.That(result.TotalComparison.Current, Is.EqualTo(13));

        // Share is of the filtered total.
        Assert.That(result.TopSources.Total, Is.EqualTo(3));
        Assert.That(result.TopSources.Items.Single().Share, Is.EqualTo(1.0).Within(1e-9));
    }

    [Test]
    public void Build_XperienceAndCustomSources_AreSeparateRankedLists()
    {
        EventLogDailyCount[] daily = [new("I", new(2026, 9, 1), 20)];
        var data = Data(daily) with
        {
            Sources = new([new("CMS.Scheduler", 12, 6), new("Acme.Crm", 8, 0)], 2),
            XperienceSources = new([new("CMS.Scheduler", 12, 6)], 1),
            CustomSources = new([new("Acme.Crm", 8, 0)], 1),
        };

        var result = EventLogReportBuilder.Build(allTypes, data, null, _ => null);

        Assert.That(result.TopSources.Items.Select(i => i.Key), Is.EqualTo(new[] { "CMS.Scheduler", "Acme.Crm" }));
        Assert.That(result.TopXperienceSources.Items.Select(i => (i.Key, i.Rank, i.Change)), Is.EqualTo(new[] { ("CMS.Scheduler", 1, (double?)1.0) }));
        Assert.That(result.TopCustomSources.Items.Select(i => (i.Key, i.Rank, i.Change)), Is.EqualTo(new[] { ("Acme.Crm", 1, (double?)null) }));
        Assert.That(result.TopCustomSources.Items.Single().PreviousValue, Is.Zero);
        Assert.That((result.TopXperienceSources.ItemCount, result.TopCustomSources.ItemCount), Is.EqualTo((1, 1)));

        // Shares stay of all events in the range, like the full list.
        Assert.That(result.TopCustomSources.Items.Single().Share, Is.EqualTo(0.4).Within(1e-9));
    }

    [Test]
    public void Build_NoCustomSources_ReturnsEmptyCustomList()
    {
        var data = Data([new("I", new(2026, 9, 1), 5)]) with
        {
            Sources = new([new("CMS.Scheduler", 5, 0)], 1),
            XperienceSources = new([new("CMS.Scheduler", 5, 0)], 1),
        };

        var result = EventLogReportBuilder.Build(allTypes, data, null, _ => null);

        Assert.That(result.TopCustomSources.Items, Is.Empty);
        Assert.That(result.TopCustomSources.ItemCount, Is.Zero);
    }

    [Test]
    public void Build_TopSourcesAndCodes_HaveChangeVsPreviousPeriod()
    {
        EventLogDailyCount[] daily = [new("I", new(2026, 9, 1), 15)];
        var sources = new EventLogTop<EventLogGroupRow>([new("Content", 10, 5), new("Scheduler", 5, 0)], 7);
        var codes = new EventLogTop<EventLogGroupRow>([new("UPDATE", 12, 16)], 2);

        var result = EventLogReportBuilder.Build(allTypes, Data(daily) with { Sources = sources, Codes = codes }, null, _ => null);

        Assert.That(result.TopSources.Items.Select(i => (i.Label, i.Value, i.PreviousValue)), Is.EqualTo(new[]
        {
            ("Content", 10, (int?)5),
            ("Scheduler", 5, (int?)0),
        }));
        Assert.That(result.TopSources.Items[0].Change, Is.EqualTo(1.0).Within(1e-9));
        Assert.That(result.TopSources.Items[1].Change, Is.Null);
        Assert.That(result.TopSources.ItemCount, Is.EqualTo(7));
        Assert.That(result.TopCodes.Items.Single().Change, Is.EqualTo(-0.25).Within(1e-9));
    }

    [Test]
    public void Build_OnlySystemEvents_ShowsSystemRow()
    {
        EventLogDailyCount[] daily = [new("I", new(2026, 9, 1), 8)];
        var users = new EventLogTop<EventLogUserRow>([new(null, null, false, 8, 4)], 1);

        var result = EventLogReportBuilder.Build(allTypes, Data(daily) with { Users = users }, null, _ => "/never");

        var row = result.TopUsers.Items.Single();
        Assert.That((row.Key, row.Label, row.Value), Is.EqualTo((EventLogReportBuilder.SystemUserKey, "System", 8)));
        Assert.That(row.AdminPath, Is.Null);
        Assert.That(row.PreviousValue, Is.EqualTo(4));
        Assert.That(row.Change, Is.EqualTo(1.0).Within(1e-9));
    }

    [Test]
    public void Build_Users_LinkExistingUsersOnly_AndFallBackToId()
    {
        EventLogDailyCount[] daily = [new("I", new(2026, 9, 1), 10)];
        var users = new EventLogTop<EventLogUserRow>(
            [
                new(53, "administrator", true, 5, 0),
                new(null, null, false, 3, 6),
                new(70, null, false, 1, 1),
                new(null, "external", false, 1, 2),
            ],
            4);

        var result = EventLogReportBuilder.Build(allTypes, Data(daily) with { Users = users }, null, id => $"/users/{id}");

        Assert.That(result.TopUsers.Items.Select(i => (i.Label, i.AdminPath)), Is.EqualTo(new[]
        {
            ("administrator", (string?)"/users/53"),
            ("System", null),
            ("external", null),
            ("User 70", null),
        }));

        // Change vs previous period; null ("New") when the previous count is 0.
        Assert.That(result.TopUsers.Items.Select(i => i.PreviousValue), Is.EqualTo(new int?[] { 0, 6, 2, 1 }));
        Assert.That(result.TopUsers.Items[0].Change, Is.Null);
        Assert.That(result.TopUsers.Items[1].Change, Is.EqualTo(-0.5).Within(1e-9));
        Assert.That(result.TopUsers.Items[2].Change, Is.EqualTo(-0.5).Within(1e-9));
        Assert.That(result.TopUsers.Items[3].Change, Is.EqualTo(0.0).Within(1e-9));
    }

    private static EventLogReportData Data(IReadOnlyList<EventLogDailyCount> daily) => EventLogReportData.Empty with { Daily = daily };
}
