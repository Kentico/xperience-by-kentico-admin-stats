using Kentico.Xperience.AdminStats.Shared;

namespace Kentico.Xperience.AdminStats.Reports.RecipientLists;

/// <summary>
/// Filter of the recipient lists report, sent by the admin client: the shared range and grouping (<see cref="StatsFilter"/>)
/// and an optional recipient list. The shared filter is wrapped, not changed, so other reports keep their filter and cache keys.
/// </summary>
public sealed record RecipientListsFilter
{
    /// <summary>
    /// Range and grouping. The channel is ignored (recipient lists have no channel). <c>null</c> means defaults.
    /// </summary>
    public StatsFilter? Range { get; init; }

    /// <summary>
    /// Optional recipient list (contact group ID). <c>null</c>, a value &lt;= 0,
    /// or an ID that is not a recipient list (checked by the query) means all lists.
    /// </summary>
    public int? RecipientListId { get; init; }

    /// <summary>
    /// Applies defaults and limits and returns a query that is safe to run.
    /// </summary>
    /// <param name="today">Current date used for the default range.</param>
    public RecipientListsQuery Normalize(DateOnly today) =>
        new((Range ?? new StatsFilter()).Normalize(today) with { ChannelId = null }, RecipientListId is > 0 ? RecipientListId : null);
}

/// <summary>
/// Normalized recipient lists filter.
/// </summary>
/// <param name="Range">Range and grouping. <see cref="StatsQuery.ChannelId"/> is always <c>null</c>.</param>
/// <param name="RecipientListId">Recipient list ID, or <c>null</c> for all lists. Unknown IDs are treated as all lists.</param>
public sealed record RecipientListsQuery(StatsQuery Range, int? RecipientListId);

/// <summary>
/// Input of the recipient lists <c>LOAD</c> page command.
/// </summary>
public sealed record RecipientListsLoadRequest
{
    /// <summary>
    /// Report filter. <c>null</c> means defaults.
    /// </summary>
    public RecipientListsFilter? Filter { get; init; }

    /// <summary>
    /// When <c>true</c>, cached data for the filter is dropped and read again from the database.
    /// </summary>
    public bool Refresh { get; init; }
}

/// <summary>
/// Recipient list shown in the list filter, by display name.
/// </summary>
public sealed record RecipientListOption(int Id, string DisplayName);

/// <summary>
/// KPIs of the range compared with the previous period (list filter applied).
/// </summary>
/// <param name="Subscriptions">Approved subscription confirmations in the period. Subscribing again counts again.</param>
/// <param name="Unsubscriptions">Unsubscriptions (revoked confirmations) in the period.</param>
/// <param name="UnsubscribeRate">
/// Unsubscriptions / subscriptions in the period (ratio). <c>null</c> for a period without subscriptions. The change is in percentage points.
/// </param>
/// <param name="Subscribers">
/// Subscribers (see <see cref="RecipientListsResult.Subscribers"/>) on the last day of the range vs the last day of the previous period.
/// A point-in-time count, not a sum over the period.
/// </param>
public sealed record RecipientListsTotals(
    StatsValueComparison Subscriptions,
    StatsValueComparison Unsubscriptions,
    StatsValueComparison UnsubscribeRate,
    StatsValueComparison Subscribers);

/// <summary>
/// Current (now, not range end) status of recipient list members, counted like the native recipient list overview.
/// With all lists, contact + list pairs are counted (a contact in two lists counts twice).
/// </summary>
/// <param name="Receiving">Members whose latest confirmation is approved and whose email is not bounced.</param>
/// <param name="Bounced">Members whose latest confirmation is approved and whose email is bounced.</param>
/// <param name="Unsubscribed">Members whose latest confirmation is not approved.</param>
/// <param name="NotConfirmed">Members without a confirmation (double opt-in pending, or added without confirmation).</param>
public sealed record RecipientListStatuses(int Receiving, int Bounced, int Unsubscribed, int NotConfirmed)
{
    public static RecipientListStatuses Empty { get; } = new(0, 0, 0, 0);

    public RecipientListStatuses Add(RecipientListStatuses other) =>
        new(Receiving + other.Receiving, Bounced + other.Bounced, Unsubscribed + other.Unsubscribed, NotConfirmed + other.NotConfirmed);
}

/// <summary>
/// One recipient list in the lists table (list filter does not apply).
/// </summary>
/// <param name="Id">Contact group ID.</param>
/// <param name="DisplayName">Display name.</param>
/// <param name="Statuses">Current statuses (now).</param>
/// <param name="Subscriptions">Subscriptions in the range.</param>
/// <param name="Unsubscriptions">Unsubscriptions in the range.</param>
/// <param name="Subscribers">Subscribers on the range end.</param>
/// <param name="PreviousSubscribers">Subscribers on the previous period end.</param>
public sealed record RecipientListSummary(
    int Id,
    string DisplayName,
    RecipientListStatuses Statuses,
    int Subscriptions,
    int Unsubscriptions,
    int Subscribers,
    int PreviousSubscribers)
{
    /// <summary>
    /// Path of the list in the native Recipient lists application, relative to the admin root. <c>null</c> when it is not available.
    /// </summary>
    public string? AdminPath { get; init; }
}

/// <summary>
/// Recipient lists report.
/// </summary>
/// <param name="From">Applied range start (inclusive).</param>
/// <param name="To">Applied range end (inclusive).</param>
/// <param name="Grouping">Applied grouping.</param>
/// <param name="RecipientListId">Applied list filter, <c>null</c> for all lists.</param>
/// <param name="ListOptions">Recipient lists for the list filter, by display name.</param>
/// <param name="Periods">Period axis of the series.</param>
/// <param name="Subscriptions">Subscriptions per period.</param>
/// <param name="Unsubscriptions">Unsubscriptions per period.</param>
/// <param name="Subscribers">
/// Subscribers at the end of each period (the range end for the last, partial period): current members whose latest confirmation of the list
/// on or before that day is approved. Membership has no history, so it is applied as current state. Bounces are not applied (current state only).
/// With all lists, distinct contacts subscribed to at least one list.
/// A point-in-time count; <see cref="StatsValueSeries.Total"/> is the value at the range end, not a sum.
/// </param>
/// <param name="Totals">KPIs vs the previous period.</param>
/// <param name="ByList">All recipient lists by receiving members (the list filter does not apply, so lists can be compared).</param>
/// <param name="Statuses">Current statuses (list filter applied).</param>
/// <param name="Available"><c>false</c> when the recipient list tables do not exist; the report is then empty.</param>
public sealed record RecipientListsResult(
    DateOnly From,
    DateOnly To,
    StatsGrouping Grouping,
    int? RecipientListId,
    IReadOnlyList<RecipientListOption> ListOptions,
    IReadOnlyList<StatsPeriod> Periods,
    StatsValueSeries Subscriptions,
    StatsValueSeries Unsubscriptions,
    StatsValueSeries Subscribers,
    RecipientListsTotals Totals,
    IReadOnlyList<RecipientListSummary> ByList,
    RecipientListStatuses Statuses,
    bool Available)
{
    /// <summary>
    /// Path of the native Recipient lists application's listing, relative to the admin root. <c>null</c> when it is not available.
    /// </summary>
    public string? RecipientListsAppPath { get; init; }

    /// <summary>
    /// When the data was read from the database. Can be older than the request when served from cache.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Recipient list events on one day (list filter applied), as returned by the SQL aggregate.
/// </summary>
/// <param name="Date">Day of the events.</param>
/// <param name="Subscriptions">Approved confirmation rows.</param>
/// <param name="Unsubscriptions">Not approved confirmation rows.</param>
/// <param name="SubscriberChange">
/// Change of the subscribers count on that day (contacts who became subscribed minus contacts who stopped being subscribed).
/// Summing the changes up to a day gives the subscribers on that day.
/// </param>
public sealed record RecipientListsDailyRow(DateOnly Date, int Subscriptions, int Unsubscriptions, int SubscriberChange);

/// <summary>
/// One recipient list with its numbers (list filter does not apply), as returned by the SQL aggregate.
/// </summary>
/// <param name="ListId">Contact group ID.</param>
/// <param name="DisplayName">Display name.</param>
/// <param name="Subscriptions">Subscriptions in the range.</param>
/// <param name="Unsubscriptions">Unsubscriptions in the range.</param>
/// <param name="Subscribers">Subscribers on the range end.</param>
/// <param name="PreviousSubscribers">Subscribers on the previous period end.</param>
/// <param name="Statuses">Current statuses (now).</param>
public sealed record RecipientListsListRow(
    int ListId,
    string? DisplayName,
    int Subscriptions,
    int Unsubscriptions,
    int Subscribers,
    int PreviousSubscribers,
    RecipientListStatuses Statuses);

/// <summary>
/// Aggregated recipient list data. <see cref="Daily"/> starts at the previous period.
/// </summary>
/// <param name="Available"><c>false</c> when the recipient list tables do not exist.</param>
/// <param name="Lists">All recipient lists, by display name.</param>
/// <param name="Daily">Events per day, previous period + range (list filter applied).</param>
/// <param name="SubscribersBeforeRange">Subscribers on the last day before the range (list filter applied).</param>
public sealed record RecipientListsReportData(
    bool Available,
    IReadOnlyList<RecipientListsListRow> Lists,
    IReadOnlyList<RecipientListsDailyRow> Daily,
    int SubscribersBeforeRange)
{
    public static RecipientListsReportData Empty { get; } = new(true, [], [], 0);

    public static RecipientListsReportData Unavailable { get; } = Empty with { Available = false };
}

/// <summary>
/// Recipient list data with the time it was read. This is the cached value.
/// </summary>
internal sealed record RecipientListsSnapshot(RecipientListsReportData Data, DateTimeOffset ReadAt);
