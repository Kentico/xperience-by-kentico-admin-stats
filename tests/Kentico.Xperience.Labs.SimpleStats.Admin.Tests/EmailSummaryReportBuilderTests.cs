using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EmailSummary;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class EmailSummaryReportBuilderTests
{
    // Previous period: Aug 2 – Aug 31.
    private static readonly StatsQuery query = new(new(2026, 9, 1), new(2026, 9, 30), StatsGrouping.Day, null);

    private static readonly EmailSummaryChannelRow channel = new(1, 7, "en");

    private static EmailSummaryEmailRow Email(int id, DateTime sendTime, EmailStatisticsValues? statistics, string? name = null) =>
        new(id, $"Email{id}", name ?? $"Newsletter #{id}", 1, "en", sendTime, "Newsletter", statistics);

    private static EmailStatisticsValues Stats(int sent, int delivered, int opens, int clicks, int? hard = 0, int unsubscribes = 0, int? spam = null) =>
        new(sent, delivered, opens, clicks, null, hard, unsubscribes, spam);

    private static EmailSummaryReportData Data(params EmailSummaryEmailRow[] emails) =>
        EmailSummaryReportData.Empty with { Channels = [channel], Emails = emails };

    [Test]
    public void Build_NoEmails_ZerosNullRatesAndEmptyLists()
    {
        var result = EmailSummaryReportBuilder.Build(query, EmailSummaryReportData.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(result.Available, Is.True);
            Assert.That(result.Periods, Has.Count.EqualTo(30));
            Assert.That(result.Emails, Is.Empty);
            Assert.That(result.Automated, Is.Empty);
            Assert.That(result.Totals.Emails.Current, Is.Zero);
            Assert.That(result.Totals.Sent.Current, Is.Zero);
            Assert.That(result.Totals.OpenRate.Current, Is.Null);
            Assert.That(result.Totals.DeliveryRate.Current, Is.Null);
            Assert.That(result.Totals.HardBounces.Current, Is.Null);
            Assert.That(result.ByOpenRate.Top.Items, Is.Empty);
            Assert.That(result.Sent.Values, Is.All.Zero);
        });
    }

    [Test]
    public void Build_Unavailable_IsFlagged()
    {
        var result = EmailSummaryReportBuilder.Build(query, EmailSummaryReportData.Unavailable);

        Assert.That(result.Available, Is.False);
        Assert.That(result.Emails, Is.Empty);
    }

    [Test]
    public void Build_EmailWithoutStatistics_ListedWithZerosAndNullRates()
    {
        var result = EmailSummaryReportBuilder.Build(query, Data(Email(1, new(2026, 9, 10, 9, 0, 0, DateTimeKind.Unspecified), null)));

        var email = result.Emails.Single();
        Assert.Multiple(() =>
        {
            Assert.That(email.HasStatistics, Is.False);
            Assert.That(email.Statistics.Sent, Is.Zero);
            Assert.That(email.Rates.OpenRate, Is.Null);
            Assert.That(email.Rates.DeliveryRate, Is.Null);
            Assert.That(result.Totals.Emails.Current, Is.EqualTo(1m));
            Assert.That(result.Totals.OpenRate.Current, Is.Null);
        });
    }

    [Test]
    public void Build_Rates_FollowStatisticsTab()
    {
        // Production example: 75 unique opens, 136 sent, 135 delivered -> tab 55.6% (list would be 55.1%).
        var result = EmailSummaryReportBuilder.Build(query, Data(Email(1, new(2026, 9, 10, 9, 0, 0, DateTimeKind.Unspecified), Stats(136, 135, 75, 20, hard: 1, unsubscribes: 1))));

        var rates = result.Emails.Single().Rates;
        Assert.Multiple(() =>
        {
            Assert.That(rates.OpenRate, Is.EqualTo(0.5556m));
            Assert.That(rates.ClickRate, Is.EqualTo(0.1481m));
            Assert.That(rates.DeliveryRate, Is.EqualTo(0.9926m));
            Assert.That(rates.UnsubscribeRate, Is.EqualTo(0.0074m));
        });
    }

    [Test]
    public void Build_RateNull_WhenDeliveredZero()
    {
        var result = EmailSummaryReportBuilder.Build(query, Data(Email(1, new(2026, 9, 10, 9, 0, 0, DateTimeKind.Unspecified), Stats(5, 0, 0, 0, hard: 5))));

        var email = result.Emails.Single();
        Assert.Multiple(() =>
        {
            Assert.That(email.Rates.OpenRate, Is.Null);
            Assert.That(email.Rates.ClickRate, Is.Null);
            Assert.That(email.Rates.DeliveryRate, Is.EqualTo(0m));
            Assert.That(result.Totals.OpenRate.Current, Is.Null);
        });
    }

    [Test]
    public void Build_RateOver100Percent_NotCapped()
    {
        // Community Portal data: opens and clicks of mailouts without a sent hit.
        var result = EmailSummaryReportBuilder.Build(query, Data(Email(1, new(2026, 9, 10, 9, 0, 0, DateTimeKind.Unspecified), Stats(68, 68, 94, 83))));

        Assert.Multiple(() =>
        {
            Assert.That(result.Emails.Single().Rates.OpenRate, Is.EqualTo(1.3824m));
            Assert.That(result.Totals.OpenRate.Current, Is.EqualTo(1.3824m));
        });
    }

    [Test]
    public void Build_AggregateRates_AreSumsNotAverages()
    {
        // 90/100 and 10/900: average of rates = 45.6%, sums = 100/1000 = 10%.
        var result = EmailSummaryReportBuilder.Build(
            query,
            Data(
                Email(1, new(2026, 9, 3, 9, 0, 0, DateTimeKind.Unspecified), Stats(100, 100, 90, 10)),
                Email(2, new(2026, 9, 17, 9, 0, 0, DateTimeKind.Unspecified), Stats(900, 900, 10, 0))));

        Assert.Multiple(() =>
        {
            Assert.That(result.Totals.OpenRate.Current, Is.EqualTo(0.1m));
            Assert.That(result.Totals.ClickRate.Current, Is.EqualTo(0.01m));
            Assert.That(result.Totals.Sent.Current, Is.EqualTo(1000m));
            Assert.That(result.Totals.Emails.Current, Is.EqualTo(2m));
        });
    }

    [Test]
    public void Build_NullBouncesAndSpam_LeftOutOfSums_NullWhenAllNull()
    {
        var result = EmailSummaryReportBuilder.Build(
            query,
            Data(
                Email(1, new(2026, 9, 3, 9, 0, 0, DateTimeKind.Unspecified), Stats(100, 99, 50, 10, hard: 1, spam: null)),
                Email(2, new(2026, 9, 17, 9, 0, 0, DateTimeKind.Unspecified), Stats(100, 100, 50, 10, hard: null, spam: null))));

        Assert.Multiple(() =>
        {
            Assert.That(result.Totals.HardBounces.Current, Is.EqualTo(1m));
            Assert.That(result.Totals.SoftBounces.Current, Is.Null);
            Assert.That(result.Totals.SpamReports.Current, Is.Null);
            Assert.That(result.Totals.SpamReports.Change, Is.Null);
            Assert.That(result.Emails.Single(e => e.Id == 2).Statistics.HardBounces, Is.Null);
        });
    }

    [Test]
    public void Build_SplitsCurrentAndPrevious_BySendDate_OnRangeEdges()
    {
        var result = EmailSummaryReportBuilder.Build(
            query,
            Data(
                Email(1, new(2026, 8, 2, 0, 0, 0, DateTimeKind.Unspecified), Stats(10, 10, 5, 1)),
                Email(2, new(2026, 8, 31, 23, 59, 59, DateTimeKind.Unspecified), Stats(20, 20, 5, 1)),
                Email(3, new(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified), Stats(40, 40, 10, 2)),
                Email(4, new(2026, 9, 30, 23, 59, 59, DateTimeKind.Unspecified), Stats(80, 80, 30, 4)),
                Email(5, new(2026, 8, 1, 23, 0, 0, DateTimeKind.Unspecified), Stats(1000, 1000, 0, 0))));

        Assert.Multiple(() =>
        {
            Assert.That(result.Emails.Select(e => e.Id), Is.EqualTo(new[] { 4, 3 }));
            Assert.That(result.Totals.Sent.Current, Is.EqualTo(120m));
            Assert.That(result.Totals.Sent.Previous, Is.EqualTo(30m));
            Assert.That(result.Totals.Emails.Previous, Is.EqualTo(2m));
            Assert.That(result.Totals.OpenRate.Current, Is.EqualTo(0.3333m));
            Assert.That(result.Totals.OpenRate.Previous, Is.EqualTo(0.3333m));
            Assert.That(result.Totals.OpenRate.Change, Is.EqualTo(0d));
        });
    }

    [Test]
    public void Build_RateChange_InPoints()
    {
        var result = EmailSummaryReportBuilder.Build(
            query,
            Data(
                Email(1, new(2026, 8, 10, 9, 0, 0, DateTimeKind.Unspecified), Stats(100, 100, 40, 10)),
                Email(2, new(2026, 9, 10, 9, 0, 0, DateTimeKind.Unspecified), Stats(100, 100, 50, 10))));

        Assert.That(result.Totals.OpenRate.Change, Is.EqualTo(0.1).Within(1e-9));
    }

    [Test]
    public void Build_Performers_TopAndBottom_WithMinimumDelivered()
    {
        var rows = Enumerable.Range(1, 12)
            .Select(i => Email(i, new(2026, 9, i, 9, 0, 0, DateTimeKind.Unspecified), Stats(100, 100, 40 + i, i)))
            // Too small to rank, although its rates are the highest.
            .Append(Email(20, new(2026, 9, 20, 9, 0, 0, DateTimeKind.Unspecified), Stats(9, EmailSummaryReportBuilder.MinDeliveredForRanking - 1, 9, 9)))
            .ToArray();

        var result = EmailSummaryReportBuilder.Build(query, Data(rows));

        Assert.Multiple(() =>
        {
            Assert.That(result.MinDeliveredForRanking, Is.EqualTo(10));
            Assert.That(result.ByOpenRate.Top.Items.Select(i => i.Key), Is.EqualTo(new[] { "12", "11", "10", "9", "8" }));
            Assert.That(result.ByOpenRate.Bottom.Items.Select(i => i.Key), Is.EqualTo(new[] { "1", "2", "3", "4", "5" }));
            Assert.That(result.ByOpenRate.Top.Items[0].Value, Is.EqualTo(0.52m));
            Assert.That(result.ByOpenRate.Top.Items[0].SecondaryValue, Is.EqualTo(100m));
            Assert.That(result.ByOpenRate.Top.ValueKind, Is.EqualTo(StatsValueKind.Ratio));
            Assert.That(result.ByOpenRate.Top.ItemCount, Is.EqualTo(12));
            Assert.That(result.ByClickRate.Top.Items[0].Key, Is.EqualTo("12"));
        });
    }

    [Test]
    public void Build_Performers_FewEmails_BottomLeavesOutTop()
    {
        var rows = Enumerable.Range(1, 7).Select(i => Email(i, new(2026, 9, i, 9, 0, 0, DateTimeKind.Unspecified), Stats(100, 100, 40 + i, i))).ToArray();

        var result = EmailSummaryReportBuilder.Build(query, Data(rows));

        Assert.Multiple(() =>
        {
            Assert.That(result.ByOpenRate.Top.Items, Has.Count.EqualTo(5));
            Assert.That(result.ByOpenRate.Bottom.Items.Select(i => i.Key), Is.EqualTo(new[] { "1", "2" }));
        });
    }

    [Test]
    public void Build_Performers_ZeroRateRanked()
    {
        var result = EmailSummaryReportBuilder.Build(query, Data(Email(1, new(2026, 9, 1, 9, 0, 0, DateTimeKind.Unspecified), Stats(100, 100, 0, 0))));

        Assert.That(result.ByOpenRate.Top.Items.Single().Value, Is.Zero);
    }

    [Test]
    public void Build_Activity_FromRows_OnPeriodAxis()
    {
        var data = EmailSummaryReportData.Empty with
        {
            Activity = [new(new(2026, 9, 1), 145, 30, 5, 1), new(new(2026, 9, 2), 0, 20, 3, 0), new(new(2026, 10, 1), 9, 9, 9, 9)],
        };

        var result = EmailSummaryReportBuilder.Build(query, data);

        Assert.Multiple(() =>
        {
            Assert.That(result.Sent.Values[0], Is.EqualTo(145m));
            Assert.That(result.UniqueOpens.Values[1], Is.EqualTo(20m));
            Assert.That(result.UniqueOpens.Total, Is.EqualTo(50m));
            Assert.That(result.UniqueClicks.Total, Is.EqualTo(8m));
            Assert.That(result.Unsubscribes.Total, Is.EqualTo(1m));
        });
    }

    [Test]
    public void Build_Activity_WeekRowsOnRangeStart()
    {
        // Sep 1 2026 is a Tuesday; SQL moves the first, partial week to the range start.
        var weekly = query with { Grouping = StatsGrouping.Week };
        var data = EmailSummaryReportData.Empty with { Activity = [new(new(2026, 9, 1), 10, 5, 1, 0), new(new(2026, 9, 7), 20, 6, 2, 0)] };

        var result = EmailSummaryReportBuilder.Build(weekly, data);

        Assert.That(result.Sent.Values.Take(2), Is.EqualTo(new[] { 10m, 20m }));
    }

    [Test]
    public void Build_UnknownChannel_MeansAll_KnownKept()
    {
        var data = Data();

        Assert.Multiple(() =>
        {
            Assert.That(EmailSummaryReportBuilder.Build(query with { ChannelId = 99 }, data).ChannelId, Is.Null);
            Assert.That(EmailSummaryReportBuilder.Build(query with { ChannelId = 7 }, data).ChannelId, Is.EqualTo(7));
        });
    }

    [Test]
    public void Build_Names_FallBackToCodeName_AndLinksUseChannelAndLanguage()
    {
        var data = Data(new EmailSummaryEmailRow(3, "CodeName", " ", 1, "es", new(2026, 9, 10, 9, 0, 0, DateTimeKind.Unspecified), null, Stats(10, 10, 1, 1))) with
        {
            Automated = [new(4, "Auto", "Welcome", "Automation", null, "en", 3, Stats(10, 10, 5, 1))],
        };
        var calls = new List<(int, int?, string?)>();

        var result = EmailSummaryReportBuilder.Build(query, data, (id, emailChannelId, language) =>
        {
            calls.Add((id, emailChannelId, language));
            return emailChannelId is null ? null : $"/stats/{id}";
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Emails.Single().Name, Is.EqualTo("CodeName"));
            Assert.That(result.Emails.Single().RecipientList, Is.Null);
            Assert.That(result.Emails.Single().StatisticsPath, Is.EqualTo("/stats/3"));
            Assert.That(result.Automated.Single().StatisticsPath, Is.Null);
            Assert.That(result.Automated.Single().SentInRange, Is.EqualTo(3));
            Assert.That(calls, Does.Contain((3, (int?)1, (string?)"es")));
        });
    }

    [Test]
    public void GetListChannel_SelectedOrOnly()
    {
        var two = EmailSummaryReportData.Empty with { Channels = [channel, new(2, 8, "en")] };

        Assert.Multiple(() =>
        {
            Assert.That(EmailSummaryReportBuilder.GetListChannel(null, Data()), Is.EqualTo(channel));
            Assert.That(EmailSummaryReportBuilder.GetListChannel(null, two), Is.Null);
            Assert.That(EmailSummaryReportBuilder.GetListChannel(8, two)?.EmailChannelId, Is.EqualTo(2));
        });
    }
}
