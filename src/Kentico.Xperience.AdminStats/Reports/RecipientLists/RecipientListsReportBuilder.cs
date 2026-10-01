using System.Globalization;

using Kentico.Xperience.AdminStats.Shared;

namespace Kentico.Xperience.AdminStats.Reports.RecipientLists;

/// <summary>
/// Turns aggregated recipient list data into the recipient lists report.
/// </summary>
internal static class RecipientListsReportBuilder
{
    public static StatsSeriesDefinition SubscriptionsSeries { get; } = new("subscriptions", "Subscriptions");

    public static StatsSeriesDefinition UnsubscriptionsSeries { get; } = new("unsubscriptions", "Unsubscriptions");

    public static StatsSeriesDefinition SubscribersSeries { get; } = new("subscribers", "Subscribers");

    /// <summary>
    /// Builds the report.
    /// </summary>
    /// <param name="query">Normalized filter. A list ID that is not in <see cref="RecipientListsReportData.Lists"/> means all lists.</param>
    /// <param name="data">Data from the start of the previous period (see <see cref="StatsComparison.GetPreviousRange"/>) to the end of the range.</param>
    /// <param name="getListPath">Returns the admin path of a recipient list, or <c>null</c>.</param>
    public static RecipientListsResult Build(RecipientListsQuery query, RecipientListsReportData data, Func<int, string?>? getListPath = null)
    {
        var range = query.Range with { ChannelId = null };
        var (previousFrom, previousTo) = StatsComparison.GetPreviousRange(range);

        // Same rule as the SQL: an unknown list means all lists.
        int? listId = query.RecipientListId is int id && data.Lists.Any(l => l.ListId == id) ? id : null;

        var subscriptions = StatsTimeSeriesBuilder.BuildValueSeries(
            range,
            data.Daily.Select(row => new StatsDailyValue(SubscriptionsSeries.Key, row.Date, row.Subscriptions)).ToList(),
            SubscriptionsSeries,
            StatsValueKind.Count);
        var unsubscriptions = StatsTimeSeriesBuilder.BuildValueSeries(
            range,
            data.Daily.Select(row => new StatsDailyValue(UnsubscriptionsSeries.Key, row.Date, row.Unsubscriptions)).ToList(),
            UnsubscriptionsSeries,
            StatsValueKind.Count);

        int subscribersBefore = Math.Max(data.SubscribersBeforeRange, 0);
        var subscribers = StatsTimeSeriesBuilder.BuildCumulativeSeries(
            range,
            data.Daily.Select(row => new StatsDailyValue(SubscribersSeries.Key, row.Date, row.SubscriberChange)),
            SubscribersSeries,
            StatsValueKind.Count,
            subscribersBefore);

        (int Subscriptions, int Unsubscriptions) Sum(DateOnly from, DateOnly to)
        {
            var rows = data.Daily.Where(row => row.Date >= from && row.Date <= to).ToList();
            return (rows.Sum(row => Math.Max(row.Subscriptions, 0)), rows.Sum(row => Math.Max(row.Unsubscriptions, 0)));
        }

        var (subscriptionCount, unsubscriptionCount) = Sum(range.From, range.To);
        var (previousSubscriptions, previousUnsubscriptions) = Sum(previousFrom, previousTo);

        var totals = new RecipientListsTotals(
            StatsValueComparison.Create(range, StatsValueKind.Count, subscriptionCount, previousSubscriptions),
            StatsValueComparison.Create(range, StatsValueKind.Count, unsubscriptionCount, previousUnsubscriptions),
            StatsValueComparison.Create(
                range,
                StatsValueKind.Ratio,
                StatsValues.Divide(unsubscriptionCount, subscriptionCount),
                StatsValues.Divide(previousUnsubscriptions, previousSubscriptions)),
            // Point in time: on the range end vs on the previous period end (= the day before the range).
            StatsValueComparison.Create(range, StatsValueKind.Count, subscribers.Total, subscribersBefore));

        var statuses = data.Lists
            .Where(row => listId is null || row.ListId == listId)
            .Aggregate(RecipientListStatuses.Empty, (sum, row) => sum.Add(Clamp(row.Statuses)));

        return new(
            range.From,
            range.To,
            range.Grouping,
            listId,
            [.. data.Lists.Select(row => new RecipientListOption(row.ListId, GetListName(row)))],
            StatsPeriods.Build(range.From, range.To, range.Grouping),
            subscriptions,
            unsubscriptions,
            subscribers,
            totals,
            BuildByList(data, getListPath),
            statuses,
            data.Available);
    }

    /// <summary>
    /// Display name of a list, else "Recipient list #ID".
    /// </summary>
    public static string GetListName(RecipientListsListRow row) =>
        string.IsNullOrWhiteSpace(row.DisplayName)
            ? string.Create(CultureInfo.InvariantCulture, $"Recipient list #{row.ListId}")
            : row.DisplayName.Trim();

    /// <summary>
    /// All lists, most receiving members first (then by name). Lists without members are kept so every list can be compared.
    /// </summary>
    private static IReadOnlyList<RecipientListSummary> BuildByList(RecipientListsReportData data, Func<int, string?>? getListPath) =>
        [.. data.Lists
            .Select(row => new RecipientListSummary(
                row.ListId,
                GetListName(row),
                Clamp(row.Statuses),
                Math.Max(row.Subscriptions, 0),
                Math.Max(row.Unsubscriptions, 0),
                Math.Max(row.Subscribers, 0),
                Math.Max(row.PreviousSubscribers, 0))
            {
                AdminPath = getListPath?.Invoke(row.ListId),
            })
            .OrderByDescending(row => row.Statuses.Receiving)
            .ThenBy(row => row.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(row => row.Id)];

    private static RecipientListStatuses Clamp(RecipientListStatuses statuses) =>
        new(
            Math.Max(statuses.Receiving, 0),
            Math.Max(statuses.Bounced, 0),
            Math.Max(statuses.Unsubscribed, 0),
            Math.Max(statuses.NotConfirmed, 0));
}
