using System.Globalization;

using Kentico.Xperience.AdminStats.Reports.Commerce;
using Kentico.Xperience.AdminStats.Shared;

namespace Kentico.Xperience.AdminStats.Reports.Customers;

/// <summary>
/// Turns aggregated customer data into the customers report.
/// </summary>
internal static class CustomersReportBuilder
{
    /// <summary>
    /// Rows of the top states and each top customers list.
    /// </summary>
    public const int TopLimit = 10;

    /// <summary>
    /// Rows of the countries list (incl. "Unknown").
    /// </summary>
    public const int CountryLimit = 15;

    public static StatsSeriesDefinition NewCustomersSeries { get; } = new("new", "New customers");

    public static StatsSeriesDefinition TotalCustomersSeries { get; } = new("total", "Total customers");

    public static StatsSeriesDefinition ActiveCustomersSeries { get; } = new("active", "Active customers");

    /// <summary>
    /// Label of customers without the chosen address or without a country on it.
    /// </summary>
    public const string UnknownCountryLabel = "Unknown";

    /// <summary>
    /// Builds the report.
    /// </summary>
    /// <param name="query">Normalized filter with a status ID that exists (or <c>null</c>).</param>
    /// <param name="data">Data from the start of the previous period (see <see cref="StatsComparison.GetPreviousRange"/>) to the end of the range.</param>
    /// <param name="statuses">Order statuses for the status filter.</param>
    /// <param name="getCustomerPath">Returns the admin path of a customer, or <c>null</c>.</param>
    public static CustomersResult Build(
        CustomersQuery query,
        CustomersReportData data,
        IReadOnlyList<CommerceOrderStatusOption> statuses,
        Func<int, string?>? getCustomerPath = null)
    {
        var range = query.Range with { ChannelId = null };
        var (previousFrom, previousTo) = StatsComparison.GetPreviousRange(range);
        var totals = data.Totals;

        var dailyValues = data.Daily
            .Select(row => new StatsDailyValue(NewCustomersSeries.Key, row.Date, row.Customers))
            .ToList();

        // Rows before the range are the previous period; the series use only the range.
        var newCustomers = StatsTimeSeriesBuilder.BuildValueSeries(range, dailyValues, NewCustomersSeries, StatsValueKind.Count);
        var totalCustomers = StatsTimeSeriesBuilder.BuildCumulativeSeries(
            range,
            dailyValues.Select(row => row with { SeriesKey = TotalCustomersSeries.Key }),
            TotalCustomersSeries,
            StatsValueKind.Count,
            totals.CustomersBeforeRange);

        // Active customers: the changes before the range add up to the count on the last day of the previous period.
        int activeBefore = Math.Max(data.ActiveChanges.Where(row => row.Date < range.From).Sum(row => row.Change), 0);
        var activeCustomers = StatsTimeSeriesBuilder.BuildCumulativeSeries(
            range,
            data.ActiveChanges.Select(row => new StatsDailyValue(ActiveCustomersSeries.Key, row.Date, row.Change)),
            ActiveCustomersSeries,
            StatsValueKind.Count,
            activeBefore);

        int SumNew(DateOnly from, DateOnly to) =>
            data.Daily.Where(row => row.Date >= from && row.Date <= to).Sum(row => Math.Max(row.Customers, 0));

        int ordering = Math.Max(totals.OrderingCustomers, 0);
        int previousOrdering = Math.Max(totals.PreviousOrderingCustomers, 0);

        var kpis = new CustomersTotals(
            StatsValueComparison.Create(range, StatsValueKind.Count, SumNew(range.From, range.To), SumNew(previousFrom, previousTo)),
            StatsValueComparison.Create(range, StatsValueKind.Count, ordering, previousOrdering),
            StatsValueComparison.Create(
                range,
                StatsValueKind.Ratio,
                StatsValues.Divide(Math.Max(totals.ReturningCustomers, 0), ordering),
                StatsValues.Divide(Math.Max(totals.PreviousReturningCustomers, 0), previousOrdering)),
            StatsValueComparison.Create(
                range,
                StatsValueKind.Amount,
                StatsValues.Divide(Math.Max(totals.Revenue, 0), ordering),
                StatsValues.Divide(Math.Max(totals.PreviousRevenue, 0), previousOrdering)),
            // Point in time: on the range end vs on the previous period end.
            StatsValueComparison.Create(range, StatsValueKind.Count, activeCustomers.Total, activeBefore));

        return new(
            range.From,
            range.To,
            range.Grouping,
            query.OrderStatusId,
            query.AddressType,
            query.ActivityWindowDays,
            statuses,
            StatsPeriods.Build(range.From, range.To, range.Grouping),
            newCustomers,
            totalCustomers,
            activeCustomers,
            kpis,
            BuildLocations(range, data.Countries, "country", ordering, data.CountryCount, CountryLimit),
            BuildLocations(range, data.States, "state", data.StateCustomers, data.StateCount, TopLimit),
            BuildTopCustomers(range, data, getCustomerPath),
            data.CommerceAvailable);
    }

    /// <summary>
    /// Display name of a customer: first + last name, else the email, else "Customer #ID".
    /// </summary>
    public static string GetCustomerName(int customerId, string? firstName, string? lastName, string? email)
    {
        string name = string.Join(' ', new[] { firstName, lastName }.Select(n => n?.Trim()).Where(n => !string.IsNullOrEmpty(n)));
        if (name.Length > 0)
        {
            return name;
        }

        return string.IsNullOrWhiteSpace(email)
            ? string.Create(CultureInfo.InvariantCulture, $"Customer #{customerId}")
            : email.Trim();
    }

    private static StatsRankedResult BuildLocations(
        StatsQuery range,
        IReadOnlyList<CustomersLocationRow> rows,
        string keyPrefix,
        int total,
        int itemCount,
        int limit)
    {
        var entries = rows.Select(row => new StatsRankedEntry(
            Key: row.Id is int id ? string.Create(CultureInfo.InvariantCulture, $"{keyPrefix}:{id}") : $"{keyPrefix}:unknown",
            Label: GetLocationLabel(row),
            SecondaryLabel: null,
            Value: Math.Max(row.Customers, 0),
            SecondaryValue: null,
            Url: null)
        {
            PreviousValue = Math.Max(row.PreviousCustomers, 0),
        });

        var result = StatsRankedBuilder.Build(range, entries, total, itemCount, limit);

        return result with { ValueKind = StatsValueKind.Count };
    }

    private static string GetLocationLabel(CustomersLocationRow row)
    {
        string? name = string.IsNullOrWhiteSpace(row.Name) ? null : row.Name.Trim();
        string? country = string.IsNullOrWhiteSpace(row.CountryName) ? null : row.CountryName.Trim();

        return (name, country) switch
        {
            (null, _) => UnknownCountryLabel,
            (_, null) => name,
            _ => $"{name}, {country}",
        };
    }

    private static CustomersTopLists BuildTopCustomers(StatsQuery range, CustomersReportData data, Func<int, string?>? getCustomerPath)
    {
        var totals = data.Totals;
        int itemCount = Math.Max(totals.OrderingCustomers, 0);

        // Paths are resolved once per customer; a customer can be in several lists.
        var paths = data.Customers
            .Select(row => row.CustomerId)
            .Distinct()
            .ToDictionary(id => id, id => getCustomerPath?.Invoke(id));

        StatsRankedResult Build(
            Func<CustomersCustomerRow, int> rank,
            Func<CustomersCustomerRow, (decimal Value, decimal Previous, decimal Secondary, decimal Tertiary)> values,
            decimal total,
            StatsValueKind valueKind,
            StatsValueKind secondaryKind,
            StatsValueKind tertiaryKind)
        {
            var entries = data.Customers
                .Where(row => rank(row) <= TopLimit)
                .OrderBy(rank)
                .Select(row =>
                {
                    var (value, previous, secondary, tertiary) = values(row);
                    return new StatsRankedEntry(
                        Key: string.Create(CultureInfo.InvariantCulture, $"customer:{row.CustomerId}"),
                        Label: GetCustomerName(row.CustomerId, row.FirstName, row.LastName, row.Email),
                        SecondaryLabel: string.IsNullOrWhiteSpace(row.Email) ? null : row.Email.Trim(),
                        Value: StatsValues.Round(Math.Max(value, 0), valueKind),
                        SecondaryValue: StatsValues.Round(Math.Max(secondary, 0), secondaryKind),
                        Url: null)
                    {
                        AdminPath = paths[row.CustomerId],
                        PreviousValue = StatsValues.Round(Math.Max(previous, 0), valueKind),
                        TertiaryValue = StatsValues.Round(Math.Max(tertiary, 0), tertiaryKind),
                    };
                });

            // SQL order (ties by customer ID) is kept.
            var result = StatsRankedBuilder.Build(range, entries, StatsValues.Round(Math.Max(total, 0), valueKind), itemCount, TopLimit, keepOrder: true);

            return result with { ValueKind = valueKind, SecondaryValueKind = secondaryKind, TertiaryValueKind = tertiaryKind };
        }

        return new(
            Build(
                row => row.RevenueRank,
                row => (row.Revenue, row.PreviousRevenue, row.Orders, row.Quantity),
                totals.Revenue,
                StatsValueKind.Amount,
                StatsValueKind.Count,
                StatsValueKind.Count),
            Build(
                row => row.OrdersRank,
                row => (row.Orders, row.PreviousOrders, row.Revenue, row.Quantity),
                totals.Orders,
                StatsValueKind.Count,
                StatsValueKind.Amount,
                StatsValueKind.Count),
            Build(
                row => row.QuantityRank,
                row => (row.Quantity, row.PreviousQuantity, row.Revenue, row.Orders),
                totals.Quantity,
                StatsValueKind.Count,
                StatsValueKind.Amount,
                StatsValueKind.Count));
    }
}
