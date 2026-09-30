using System.Text.Json.Serialization;

using Kentico.Xperience.AdminStats.Reports.Commerce;
using Kentico.Xperience.AdminStats.Shared;

namespace Kentico.Xperience.AdminStats.Reports.Customers;

/// <summary>
/// Order address used for the customer location tiles (<c>CMS.Commerce.OrderAddressType</c>).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<CustomersAddressType>))]
public enum CustomersAddressType
{
    /// <summary><c>OrderAddressType.Billing</c>.</summary>
    Billing,

    /// <summary><c>OrderAddressType.Shipping</c>.</summary>
    Shipping,
}

/// <summary>
/// Activity windows of the "active customers" series: a customer is active on a day when they have an order in the window ending that day.
/// </summary>
public static class CustomersActivityWindow
{
    /// <summary>Default window (days).</summary>
    public const int Default = 90;

    /// <summary>Allowed windows (days).</summary>
    public static IReadOnlyList<int> Allowed { get; } = [30, 60, 90, 180];

    /// <summary>
    /// Returns <paramref name="days"/> when it is one of <see cref="Allowed"/>, else <see cref="Default"/>.
    /// </summary>
    public static int Normalize(int? days) => days is int d && Allowed.Contains(d) ? d : Default;
}

/// <summary>
/// Filter of the customers report, sent by the admin client: the shared range and grouping (<see cref="StatsFilter"/>),
/// an optional order status and the address type of the location tiles. The shared filter is wrapped, not changed
/// (same approach as the orders and revenue report), so other reports keep their filter and cache keys.
/// </summary>
public sealed record CustomersFilter
{
    /// <summary>
    /// Range and grouping. The channel is ignored (customers and orders have no channel). <c>null</c> means defaults.
    /// </summary>
    public StatsFilter? Range { get; init; }

    /// <summary>
    /// Optional order status ID (<c>Commerce_OrderStatus.OrderStatusID</c>). <c>null</c>, a value &lt;= 0,
    /// or an ID that does not exist (checked by the report service) means all statuses.
    /// </summary>
    public int? OrderStatusId { get; init; }

    /// <summary>
    /// Address of the location tiles. <c>null</c> or an unknown value means <see cref="CustomersAddressType.Billing"/>.
    /// </summary>
    public CustomersAddressType? AddressType { get; init; }

    /// <summary>
    /// Activity window (days) of the active customers series, one of <see cref="CustomersActivityWindow.Allowed"/>.
    /// <c>null</c> or another value means <see cref="CustomersActivityWindow.Default"/>.
    /// </summary>
    public int? ActivityWindowDays { get; init; }

    /// <summary>
    /// Applies defaults and limits and returns a query that is safe to run.
    /// </summary>
    /// <param name="today">Current date used for the default range.</param>
    public CustomersQuery Normalize(DateOnly today) =>
        new(
            (Range ?? new StatsFilter()).Normalize(today) with { ChannelId = null },
            OrderStatusId is > 0 ? OrderStatusId : null,
            AddressType is { } type && Enum.IsDefined(type) ? type : CustomersAddressType.Billing,
            CustomersActivityWindow.Normalize(ActivityWindowDays));
}

/// <summary>
/// Normalized customers filter.
/// </summary>
/// <param name="Range">Range and grouping. <see cref="StatsQuery.ChannelId"/> is always <c>null</c>.</param>
/// <param name="OrderStatusId">Order status ID, or <c>null</c> for all statuses. Unknown IDs are treated as all statuses by the service.</param>
/// <param name="AddressType">Address of the location tiles.</param>
/// <param name="ActivityWindowDays">Activity window (days) of the active customers series.</param>
public sealed record CustomersQuery(
    StatsQuery Range,
    int? OrderStatusId,
    CustomersAddressType AddressType,
    int ActivityWindowDays = CustomersActivityWindow.Default);

/// <summary>
/// Input of the customers <c>LOAD</c> page command.
/// </summary>
public sealed record CustomersLoadRequest
{
    /// <summary>
    /// Report filter. <c>null</c> means defaults.
    /// </summary>
    public CustomersFilter? Filter { get; init; }

    /// <summary>
    /// When <c>true</c>, cached data for the filter is dropped and read again from the database.
    /// </summary>
    public bool Refresh { get; init; }
}

/// <summary>
/// KPIs of the range compared with the previous period.
/// </summary>
/// <param name="NewCustomers">Customers created in the period (status filter does not apply).</param>
/// <param name="OrderingCustomers">Customers with at least one order in the period (status filter applied).</param>
/// <param name="ReturningShare">
/// Share (ratio) of ordering customers that also have an order before the period (any time, status filter applied).
/// <c>null</c> for a period without ordering customers. The change is in percentage points.
/// </param>
/// <param name="RevenuePerCustomer">Revenue (order grand totals) / ordering customers. <c>null</c> for a period without ordering customers.</param>
/// <param name="ActiveCustomers">
/// Active customers (see <see cref="CustomersResult.ActiveCustomers"/>) on the last day of the range vs the last day of the previous period.
/// A point-in-time count, not a sum over the period.
/// </param>
public sealed record CustomersTotals(
    StatsValueComparison NewCustomers,
    StatsValueComparison OrderingCustomers,
    StatsValueComparison ReturningShare,
    StatsValueComparison RevenuePerCustomer,
    StatsValueComparison ActiveCustomers);

/// <summary>
/// Top ordering customers in the range, ranked three ways (status filter applied).
/// Every list has value = the ranked metric, with its previous period value and change.
/// </summary>
/// <param name="ByRevenue">Value = revenue, secondary value = orders, tertiary value = items.</param>
/// <param name="ByOrders">Value = orders, secondary value = revenue, tertiary value = items.</param>
/// <param name="ByQuantity">Value = items, secondary value = revenue, tertiary value = orders.</param>
public sealed record CustomersTopLists(
    StatsRankedResult ByRevenue,
    StatsRankedResult ByOrders,
    StatsRankedResult ByQuantity);

/// <summary>
/// Customers report.
/// </summary>
/// <param name="From">Applied range start (inclusive).</param>
/// <param name="To">Applied range end (inclusive).</param>
/// <param name="Grouping">Applied grouping.</param>
/// <param name="OrderStatusId">Applied status filter, <c>null</c> for all statuses.</param>
/// <param name="AddressType">Applied address type of the location tiles.</param>
/// <param name="ActivityWindowDays">Applied activity window (days) of <paramref name="ActiveCustomers"/>.</param>
/// <param name="Statuses">Order statuses for the status filter, in status order.</param>
/// <param name="Periods">Period axis of <paramref name="NewCustomers"/> and <paramref name="TotalCustomers"/>.</param>
/// <param name="NewCustomers">Customers created per period.</param>
/// <param name="TotalCustomers">Customers at the end of each period (running total, see <see cref="StatsTimeSeriesBuilder.BuildCumulativeSeries"/>).</param>
/// <param name="ActiveCustomers">
/// Active customers at the end of each period (the range end for the last, partial period): customers with at least one order
/// (status filter applied) in the <paramref name="ActivityWindowDays"/> days up to and including that day.
/// A point-in-time count; <see cref="StatsValueSeries.Total"/> is the value at the range end, not a sum.
/// </param>
/// <param name="Totals">KPIs vs the previous period.</param>
/// <param name="ByCountry">
/// Ordering customers per country of the chosen address on their most recent order in the range, with the previous period count and change.
/// Customers without that address or without a country are one "Unknown" row.
/// </param>
/// <param name="TopStates">
/// Ordering customers per state ("State, Country") like <paramref name="ByCountry"/>. Customers without a state are left out.
/// </param>
/// <param name="TopCustomers">Top ordering customers by revenue, orders and items.</param>
/// <param name="CommerceAvailable"><c>false</c> when the commerce tables do not exist; the report is then empty.</param>
public sealed record CustomersResult(
    DateOnly From,
    DateOnly To,
    StatsGrouping Grouping,
    int? OrderStatusId,
    CustomersAddressType AddressType,
    int ActivityWindowDays,
    IReadOnlyList<CommerceOrderStatusOption> Statuses,
    IReadOnlyList<StatsPeriod> Periods,
    StatsValueSeries NewCustomers,
    StatsValueSeries TotalCustomers,
    StatsValueSeries ActiveCustomers,
    CustomersTotals Totals,
    StatsRankedResult ByCountry,
    StatsRankedResult TopStates,
    CustomersTopLists TopCustomers,
    bool CommerceAvailable)
{
    /// <summary>
    /// Path of the native Customers application's listing, relative to the admin root. <c>null</c> when it is not available.
    /// </summary>
    public string? CustomersPath { get; init; }

    /// <summary>
    /// When the data was read from the database. Can be older than the request when served from cache.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Customers created on one day, as returned by the SQL aggregate.
/// </summary>
public sealed record CustomersDailyRow(DateOnly Date, int Customers);

/// <summary>
/// Change of the active customers count on one day: customers who became active (first order after a gap of at least the window)
/// minus customers who stopped being active (window days after their last order). Summing the changes up to a day gives the active customers on that day.
/// </summary>
public sealed record CustomersActiveChangeRow(DateOnly Date, int Change);

/// <summary>
/// Customer and order totals of the range and the previous period (status filter applied, except <paramref name="CustomersBeforeRange"/>).
/// </summary>
/// <param name="CustomersBeforeRange">Customers created before the range start (start of the total customers line).</param>
/// <param name="OrderingCustomers">Customers with an order in the range.</param>
/// <param name="PreviousOrderingCustomers">Customers with an order in the previous period.</param>
/// <param name="ReturningCustomers">Ordering customers of the range with an order before the range.</param>
/// <param name="PreviousReturningCustomers">Ordering customers of the previous period with an order before the previous period.</param>
/// <param name="Revenue">Sum of order grand totals in the range.</param>
/// <param name="PreviousRevenue">Sum of order grand totals in the previous period.</param>
/// <param name="Orders">Orders in the range.</param>
/// <param name="Quantity">Sum of order item quantities in the range.</param>
public sealed record CustomersTotalsRow(
    int CustomersBeforeRange,
    int OrderingCustomers,
    int PreviousOrderingCustomers,
    int ReturningCustomers,
    int PreviousReturningCustomers,
    decimal Revenue,
    decimal PreviousRevenue,
    int Orders,
    decimal Quantity)
{
    public static CustomersTotalsRow Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0);
}

/// <summary>
/// Ordering customers of one location (country or state) in the range and in the previous period.
/// </summary>
/// <param name="Id">Country or state ID. <c>null</c> for customers without a country (the "Unknown" country row).</param>
/// <param name="Name">Country or state display name. <c>null</c> when unknown.</param>
/// <param name="CountryName">Country display name of a state. <c>null</c> for countries.</param>
/// <param name="Customers">Ordering customers in the range.</param>
/// <param name="PreviousCustomers">Ordering customers in the previous period.</param>
public sealed record CustomersLocationRow(int? Id, string? Name, string? CountryName, int Customers, int PreviousCustomers);

/// <summary>
/// One ordering customer of the range: name snapshot, revenue, orders and item quantity in the range and the previous period,
/// and its 1-based positions in the three top lists (by revenue, orders and item quantity).
/// </summary>
public sealed record CustomersCustomerRow(
    int CustomerId,
    string? FirstName,
    string? LastName,
    string? Email,
    decimal Revenue,
    int Orders,
    decimal Quantity,
    decimal PreviousRevenue,
    int PreviousOrders,
    decimal PreviousQuantity,
    int RevenueRank,
    int OrdersRank,
    int QuantityRank);

/// <summary>
/// Aggregated customer data. <see cref="Daily"/> starts at the previous period.
/// </summary>
/// <param name="CommerceAvailable"><c>false</c> when the commerce tables do not exist.</param>
/// <param name="Daily">Customers created per day, previous period + range.</param>
/// <param name="Totals">Customer and order totals.</param>
/// <param name="Countries">Top countries by ordering customers in the range.</param>
/// <param name="CountryCount">Number of countries (incl. "Unknown") with ordering customers in the range.</param>
/// <param name="States">Top states by ordering customers in the range.</param>
/// <param name="StateCount">Number of states with ordering customers in the range.</param>
/// <param name="StateCustomers">Ordering customers in the range with a state.</param>
/// <param name="Customers">Customers in any of the top lists.</param>
public sealed record CustomersReportData(
    bool CommerceAvailable,
    IReadOnlyList<CustomersDailyRow> Daily,
    CustomersTotalsRow Totals,
    IReadOnlyList<CustomersLocationRow> Countries,
    int CountryCount,
    IReadOnlyList<CustomersLocationRow> States,
    int StateCount,
    int StateCustomers,
    IReadOnlyList<CustomersCustomerRow> Customers)
{
    /// <summary>
    /// Changes of the active customers count per day (status filter applied). Changes before the last day of the previous period
    /// only add up to the count on that day.
    /// </summary>
    public IReadOnlyList<CustomersActiveChangeRow> ActiveChanges { get; init; } = [];

    public static CustomersReportData Empty { get; } = new(true, [], CustomersTotalsRow.Empty, [], 0, [], 0, 0, []);

    public static CustomersReportData Unavailable { get; } = Empty with { CommerceAvailable = false };
}

/// <summary>
/// Customer data with the time it was read. This is the cached value.
/// </summary>
internal sealed record CustomersSnapshot(CustomersReportData Data, DateTimeOffset ReadAt);
