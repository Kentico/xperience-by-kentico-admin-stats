using Kentico.Xperience.AdminStats.Reports.RecipientLists;
using Kentico.Xperience.AdminStats.Shared;

namespace Kentico.Xperience.AdminStats.Tests;

public class RecipientListsReportBuilderTests
{
    // Previous period: Aug 2 – Aug 31.
    private static readonly RecipientListsQuery query = new(new(new(2026, 9, 1), new(2026, 9, 30), StatsGrouping.Day, null), null);

    private static readonly RecipientListsListRow newsletter = new(1, "Newsletter", 10, 2, 40, 30, new(35, 2, 5, 3));

    private static readonly RecipientListsListRow productNews = new(2, "Product news", 3, 1, 12, 10, new(12, 0, 1, 4));

    [Test]
    public void Build_NoLists_ZerosAndNullRate()
    {
        var result = RecipientListsReportBuilder.Build(query, RecipientListsReportData.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(result.Available, Is.True);
            Assert.That(result.Periods, Has.Count.EqualTo(30));
            Assert.That(result.ListOptions, Is.Empty);
            Assert.That(result.Subscriptions.Values, Is.All.Zero);
            Assert.That(result.Subscribers.Values, Is.All.Zero);
            Assert.That(result.Totals.Subscriptions.Current, Is.Zero);
            Assert.That(result.Totals.UnsubscribeRate.Current, Is.Null);
            Assert.That(result.Totals.UnsubscribeRate.Change, Is.Null);
            Assert.That(result.ByList, Is.Empty);
            Assert.That(result.Statuses, Is.EqualTo(RecipientListStatuses.Empty));
        });
    }

    [Test]
    public void Build_Unavailable_IsEmptyAndFlagged()
    {
        var result = RecipientListsReportBuilder.Build(query, RecipientListsReportData.Unavailable);

        Assert.That(result.Available, Is.False);
        Assert.That(result.Subscribers.Total, Is.Zero);
    }

    [Test]
    public void Build_ListWithoutMembers_ListedWithZeros()
    {
        var data = RecipientListsReportData.Empty with { Lists = [new(3, "Unused", 0, 0, 0, 0, RecipientListStatuses.Empty)] };

        var result = RecipientListsReportBuilder.Build(query, data);

        Assert.Multiple(() =>
        {
            Assert.That(result.ListOptions.Single(), Is.EqualTo(new RecipientListOption(3, "Unused")));
            Assert.That(result.ByList.Single().Statuses, Is.EqualTo(RecipientListStatuses.Empty));
            Assert.That(result.Totals.UnsubscribeRate.Current, Is.Null);
        });
    }

    [Test]
    public void Build_EventsAndRate_RangeOnly_ChangeInPoints()
    {
        var data = RecipientListsReportData.Empty with
        {
            Lists = [newsletter],
            Daily = [new(new(2026, 8, 20), 10, 1, 9), new(new(2026, 9, 2), 6, 3, 3), new(new(2026, 9, 3), 4, 0, 4)],
        };

        var result = RecipientListsReportBuilder.Build(query, data);

        Assert.Multiple(() =>
        {
            Assert.That(result.Subscriptions.Total, Is.EqualTo(10m));
            Assert.That(result.Unsubscriptions.Total, Is.EqualTo(3m));
            Assert.That(result.Subscriptions.Values[1], Is.EqualTo(6m));
            Assert.That(result.Totals.Subscriptions.Previous, Is.EqualTo(10m));
            Assert.That(result.Totals.Unsubscriptions.Previous, Is.EqualTo(1m));
            Assert.That(result.Totals.UnsubscribeRate.Current, Is.EqualTo(0.3m));
            Assert.That(result.Totals.UnsubscribeRate.Previous, Is.EqualTo(0.1m));
            Assert.That(result.Totals.UnsubscribeRate.Change, Is.EqualTo(0.2).Within(1e-9));
        });
    }

    [Test]
    public void Build_UnsubscriptionsWithoutSubscriptions_RateNull()
    {
        var data = RecipientListsReportData.Empty with { Daily = [new(new(2026, 9, 2), 0, 2, 0)] };

        var result = RecipientListsReportBuilder.Build(query, data);

        Assert.Multiple(() =>
        {
            Assert.That(result.Totals.Unsubscriptions.Current, Is.EqualTo(2m));
            Assert.That(result.Totals.UnsubscribeRate.Current, Is.Null);
        });
    }

    [Test]
    public void Build_Subscribers_PointInTimeFromStartAndChanges()
    {
        var data = RecipientListsReportData.Empty with
        {
            Lists = [newsletter],
            SubscribersBeforeRange = 20,
            // Subscribe -> unsubscribe -> subscribe again shows as +1, -1, +1; changes before the range are already in SubscribersBeforeRange.
            Daily = [new(new(2026, 8, 20), 5, 0, 5), new(new(2026, 9, 2), 1, 0, 1), new(new(2026, 9, 9), 0, 1, -1), new(new(2026, 9, 15), 1, 0, 1)],
        };

        var result = RecipientListsReportBuilder.Build(query with { Range = query.Range with { Grouping = StatsGrouping.Week } }, data);

        Assert.Multiple(() =>
        {
            Assert.That(result.Subscribers.Values[0], Is.EqualTo(21m));
            Assert.That(result.Subscribers.Values[1], Is.EqualTo(20m));
            Assert.That(result.Subscribers.Values[^1], Is.EqualTo(21m));
            Assert.That(result.Subscribers.Total, Is.EqualTo(21m));
            Assert.That(result.Totals.Subscribers.Current, Is.EqualTo(21m));
            Assert.That(result.Totals.Subscribers.Previous, Is.EqualTo(20m));
        });
    }

    [Test]
    public void Build_Subscribers_NeverNegative()
    {
        var data = RecipientListsReportData.Empty with { Daily = [new(new(2026, 9, 2), 0, 1, -1)] };

        var result = RecipientListsReportBuilder.Build(query, data);

        Assert.That(result.Subscribers.Values, Is.All.GreaterThanOrEqualTo(0m));
    }

    [Test]
    public void Build_KnownList_FiltersStatuses_UnknownMeansAll_SumsPairs()
    {
        var data = RecipientListsReportData.Empty with { Lists = [newsletter, productNews] };

        var known = RecipientListsReportBuilder.Build(query with { RecipientListId = 2 }, data);
        var unknown = RecipientListsReportBuilder.Build(query with { RecipientListId = 99 }, data);

        Assert.Multiple(() =>
        {
            Assert.That(known.RecipientListId, Is.EqualTo(2));
            Assert.That(known.Statuses, Is.EqualTo(new RecipientListStatuses(12, 0, 1, 4)));
            // The lists table ignores the filter.
            Assert.That(known.ByList, Has.Count.EqualTo(2));
            Assert.That(unknown.RecipientListId, Is.Null);
            // All lists: contact + list pairs.
            Assert.That(unknown.Statuses, Is.EqualTo(new RecipientListStatuses(47, 2, 6, 7)));
        });
    }

    [Test]
    public void Build_ByList_MostReceivingFirst_WithEventsSubscribersAndLinks()
    {
        var data = RecipientListsReportData.Empty with { Lists = [productNews, newsletter] };

        var result = RecipientListsReportBuilder.Build(query, data, id => $"/list/{id}");
        var first = result.ByList[0];

        Assert.Multiple(() =>
        {
            Assert.That(result.ByList.Select(l => l.DisplayName), Is.EqualTo(new[] { "Newsletter", "Product news" }));
            Assert.That(first.Statuses.Receiving, Is.EqualTo(35));
            Assert.That(first.Subscriptions, Is.EqualTo(10));
            Assert.That(first.Unsubscriptions, Is.EqualTo(2));
            Assert.That(first.Subscribers, Is.EqualTo(40));
            Assert.That(first.PreviousSubscribers, Is.EqualTo(30));
            Assert.That(first.AdminPath, Is.EqualTo("/list/1"));
        });
    }

    [Test]
    public void Build_ListWithoutName_GetsIdLabel()
    {
        var data = RecipientListsReportData.Empty with { Lists = [new(7, " ", 1, 0, 1, 0, RecipientListStatuses.Empty)] };

        var result = RecipientListsReportBuilder.Build(query, data);

        Assert.That(result.ListOptions.Single().DisplayName, Is.EqualTo("Recipient list #7"));
    }
}
