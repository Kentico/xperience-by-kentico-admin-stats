using Kentico.Xperience.AdminStats.Reports.Commerce;
using Kentico.Xperience.AdminStats.Reports.Customers;
using Kentico.Xperience.AdminStats.Shared;

namespace Kentico.Xperience.AdminStats.Tests;

public class CustomersReportBuilderTests
{
    private static readonly StatsQuery range = new(new(2026, 9, 1), new(2026, 9, 30), StatsGrouping.Day, null);
    private static readonly CustomersQuery query = new(range, null, CustomersAddressType.Billing);
    private static readonly CommerceOrderStatusOption[] statuses = [new(4, "Pending"), new(1, "Fulfilled")];

    [Test]
    public void Build_NoCustomers_EmptySeries_NullRatiosAndChanges()
    {
        var result = CustomersReportBuilder.Build(query, CustomersReportData.Empty, statuses);

        Assert.That(result.CommerceAvailable, Is.True);
        Assert.That(result.Periods, Has.Count.EqualTo(30));
        Assert.That(result.NewCustomers.Values, Has.Count.EqualTo(30).And.All.Zero);
        Assert.That(result.TotalCustomers.Values, Has.Count.EqualTo(30).And.All.Zero);
        Assert.That(result.Totals.NewCustomers.Current, Is.Zero);
        Assert.That(result.Totals.NewCustomers.Change, Is.Null);
        Assert.That(result.Totals.OrderingCustomers.Current, Is.Zero);
        Assert.That(result.Totals.ReturningShare.Kind, Is.EqualTo(StatsValueKind.Ratio));
        Assert.That(result.Totals.ReturningShare.Current, Is.Null);
        Assert.That(result.Totals.ReturningShare.Change, Is.Null);
        Assert.That(result.Totals.RevenuePerCustomer.Kind, Is.EqualTo(StatsValueKind.Amount));
        Assert.That(result.Totals.RevenuePerCustomer.Current, Is.Null);
        Assert.That(result.ByCountry.Items, Is.Empty);
        Assert.That(result.TopStates.Items, Is.Empty);
        Assert.That(result.TopCustomers.ByRevenue.Items, Is.Empty);
        Assert.That(result.Statuses, Is.EqualTo(statuses));
    }

    [Test]
    public void Build_CommerceUnavailable_IsFlagged()
    {
        var result = CustomersReportBuilder.Build(query, CustomersReportData.Unavailable, []);

        Assert.That(result.CommerceAvailable, Is.False);
        Assert.That(result.Totals.OrderingCustomers.Current, Is.Zero);
    }

    [Test]
    public void Build_NewCustomers_SplitsPeriods_TotalLineStartsWithEarlierCustomers()
    {
        var data = CustomersReportData.Empty with
        {
            Daily =
            [
                new(new(2026, 8, 15), 4),    // previous period
                new(new(2026, 9, 1), 2),
                new(new(2026, 9, 3), 3),
                new(new(2026, 10, 1), 9),    // after the range, ignored
            ],
            Totals = CustomersTotalsRow.Empty with { CustomersBeforeRange = 100 },
        };

        var result = CustomersReportBuilder.Build(query, data, statuses);

        Assert.That(result.NewCustomers.Values[0], Is.EqualTo(2m));
        Assert.That(result.NewCustomers.Total, Is.EqualTo(5m));
        Assert.That(result.TotalCustomers.Values[0], Is.EqualTo(102m));
        Assert.That(result.TotalCustomers.Values[1], Is.EqualTo(102m));
        Assert.That(result.TotalCustomers.Values[2], Is.EqualTo(105m));
        Assert.That(result.TotalCustomers.Values[29], Is.EqualTo(105m));
        Assert.That(result.TotalCustomers.Total, Is.EqualTo(105m));
        Assert.That(result.Totals.NewCustomers.Current, Is.EqualTo(5m));
        Assert.That(result.Totals.NewCustomers.Previous, Is.EqualTo(4m));
        Assert.That(result.Totals.NewCustomers.Change, Is.EqualTo(0.25).Within(1e-9));
    }

    [Test]
    public void Build_TotalCustomers_FollowsGrouping()
    {
        var data = CustomersReportData.Empty with
        {
            Daily = [new(new(2026, 9, 1), 2), new(new(2026, 9, 20), 3)],
            Totals = CustomersTotalsRow.Empty with { CustomersBeforeRange = 10 },
        };

        var result = CustomersReportBuilder.Build(query with { Range = range with { Grouping = StatsGrouping.Month } }, data, statuses);

        Assert.That(result.NewCustomers.Values, Is.EqualTo(new[] { 5m }));
        Assert.That(result.TotalCustomers.Values, Is.EqualTo(new[] { 15m }));
    }

    [Test]
    public void Build_Kpis_ReturningShareAndRevenuePerCustomer()
    {
        var data = CustomersReportData.Empty with
        {
            Totals = new(0, OrderingCustomers: 8, PreviousOrderingCustomers: 5, ReturningCustomers: 2, PreviousReturningCustomers: 0, Revenue: 100m, PreviousRevenue: 40m, Orders: 10, Quantity: 20m),
        };

        var result = CustomersReportBuilder.Build(query, data, statuses);

        Assert.That(result.Totals.OrderingCustomers.Current, Is.EqualTo(8m));
        Assert.That(result.Totals.OrderingCustomers.Change, Is.EqualTo(0.6).Within(1e-9));
        Assert.That(result.Totals.ReturningShare.Current, Is.EqualTo(0.25m));
        Assert.That(result.Totals.ReturningShare.Previous, Is.Zero);
        // Percentage points, also when the previous share is 0.
        Assert.That(result.Totals.ReturningShare.Change, Is.EqualTo(0.25).Within(1e-9));
        Assert.That(result.Totals.RevenuePerCustomer.Current, Is.EqualTo(12.50m));
        Assert.That(result.Totals.RevenuePerCustomer.Previous, Is.EqualTo(8m));
    }

    [Test]
    public void Build_PreviousWithoutOrderingCustomers_RatioAndAmountNull()
    {
        var data = CustomersReportData.Empty with
        {
            Totals = CustomersTotalsRow.Empty with { OrderingCustomers = 3, ReturningCustomers = 1, Revenue = 30m },
        };

        var result = CustomersReportBuilder.Build(query, data, statuses);

        Assert.That(result.Totals.OrderingCustomers.Change, Is.Null);
        Assert.That(result.Totals.ReturningShare.Current, Is.EqualTo(0.3333m));
        Assert.That(result.Totals.ReturningShare.Previous, Is.Null);
        Assert.That(result.Totals.ReturningShare.Change, Is.Null);
        Assert.That(result.Totals.RevenuePerCustomer.Previous, Is.Null);
        Assert.That(result.Totals.RevenuePerCustomer.Change, Is.Null);
    }

    [Test]
    public void Build_Countries_UnknownRow_ShareOfOrderingCustomers_AndChange()
    {
        var data = CustomersReportData.Empty with
        {
            Totals = CustomersTotalsRow.Empty with { OrderingCustomers = 10 },
            Countries =
            [
                new(271, "USA", null, 6, 3),
                new(null, null, null, 3, 0),
                new(309, "Canada", null, 1, 1),
            ],
            CountryCount = 3,
        };

        var result = CustomersReportBuilder.Build(query, data, statuses);

        Assert.That(result.ByCountry.Items.Select(i => i.Label), Is.EqualTo(new[] { "USA", "Unknown", "Canada" }));
        Assert.That(result.ByCountry.Items.Select(i => i.Key), Is.EqualTo(new[] { "country:271", "country:unknown", "country:309" }));
        Assert.That(result.ByCountry.Items[0].Share, Is.EqualTo(0.6).Within(1e-9));
        Assert.That(result.ByCountry.Items[0].Change, Is.EqualTo(1.0).Within(1e-9));
        Assert.That(result.ByCountry.Items[1].PreviousValue, Is.Zero);
        Assert.That(result.ByCountry.Items[1].Change, Is.Null);
        Assert.That(result.ByCountry.Total, Is.EqualTo(10m));
        Assert.That(result.ByCountry.ValueKind, Is.EqualTo(StatsValueKind.Count));
    }

    [Test]
    public void Build_States_LabelWithCountry_ShareOfCustomersWithState()
    {
        var data = CustomersReportData.Empty with
        {
            Totals = CustomersTotalsRow.Empty with { OrderingCustomers = 10 },
            States = [new(73, "California", "USA", 3, 1), new(5, "Ontario", null, 1, 0)],
            StateCount = 2,
            StateCustomers = 4,
        };

        var result = CustomersReportBuilder.Build(query, data, statuses);

        Assert.That(result.TopStates.Items.Select(i => i.Label), Is.EqualTo(new[] { "California, USA", "Ontario" }));
        Assert.That(result.TopStates.Items[0].Share, Is.EqualTo(0.75).Within(1e-9));
        Assert.That(result.TopStates.Total, Is.EqualTo(4m));
    }

    [Test]
    public void Build_TopCustomers_ThreeListsInSqlOrder_WithValuesAndLinks()
    {
        var data = CustomersReportData.Empty with
        {
            Totals = CustomersTotalsRow.Empty with { OrderingCustomers = 3, Revenue = 600m, Orders = 13, Quantity = 30m },
            Customers =
            [
                new(10, "Ann", "Big", "ann@example.com", 500m, 1, 2m, 250m, 1, 1m, RevenueRank: 1, OrdersRank: 3, QuantityRank: 3),
                new(20, null, null, "many@example.com", 60m, 10, 8m, 0m, 0, 0m, RevenueRank: 2, OrdersRank: 1, QuantityRank: 2),
                new(30, null, null, null, 40m, 2, 20m, 40m, 2, 10m, RevenueRank: 3, OrdersRank: 2, QuantityRank: 1),
            ],
        };

        var result = CustomersReportBuilder.Build(query, data, statuses, id => $"/customers/{id}");
        var top = result.TopCustomers;

        Assert.That(top.ByRevenue.Items.Select(i => i.Label), Is.EqualTo(new[] { "Ann Big", "many@example.com", "Customer #30" }));
        Assert.That(top.ByOrders.Items.Select(i => i.Key), Is.EqualTo(new[] { "customer:20", "customer:30", "customer:10" }));
        Assert.That(top.ByQuantity.Items.Select(i => i.Key), Is.EqualTo(new[] { "customer:30", "customer:20", "customer:10" }));

        var ann = top.ByRevenue.Items[0];
        Assert.That((ann.Value, ann.SecondaryValue, ann.TertiaryValue), Is.EqualTo((500m, (decimal?)1m, (decimal?)2m)));
        Assert.That(ann.SecondaryLabel, Is.EqualTo("ann@example.com"));
        Assert.That(ann.PreviousValue, Is.EqualTo(250m));
        Assert.That(ann.Change, Is.EqualTo(1.0).Within(1e-9));
        Assert.That(ann.AdminPath, Is.EqualTo("/customers/10"));
        Assert.That(ann.Share, Is.EqualTo(500.0 / 600).Within(1e-9));

        var many = top.ByOrders.Items[0];
        Assert.That((many.Value, many.SecondaryValue, many.TertiaryValue), Is.EqualTo((10m, (decimal?)60m, (decimal?)8m)));
        Assert.That(many.Change, Is.Null);
        Assert.That(top.ByOrders.Items[1].SecondaryLabel, Is.Null);

        var items = top.ByQuantity.Items[0];
        Assert.That((items.Value, items.SecondaryValue, items.TertiaryValue), Is.EqualTo((20m, (decimal?)40m, (decimal?)2m)));

        Assert.That(top.ByRevenue.ValueKind, Is.EqualTo(StatsValueKind.Amount));
        Assert.That(top.ByRevenue.SecondaryValueKind, Is.EqualTo(StatsValueKind.Count));
        Assert.That(top.ByOrders.SecondaryValueKind, Is.EqualTo(StatsValueKind.Amount));
        Assert.That(top.ByQuantity.TertiaryValueKind, Is.EqualTo(StatsValueKind.Count));
        Assert.That(top.ByRevenue.ItemCount, Is.EqualTo(3));
        Assert.That(top.ByOrders.Total, Is.EqualTo(13m));
    }

    [Test]
    public void Build_TopCustomers_OnlyRowsWithinTheLimitPerList()
    {
        var rows = Enumerable.Range(1, 12)
            .Select(i => new CustomersCustomerRow(i, "C", i.ToString(System.Globalization.CultureInfo.InvariantCulture), null, 100 - i, 1, 1m, 0m, 0, 0m, i, 13 - i, i))
            .ToList();

        var result = CustomersReportBuilder.Build(query, CustomersReportData.Empty with { Customers = rows }, statuses);

        Assert.That(result.TopCustomers.ByRevenue.Items, Has.Count.EqualTo(CustomersReportBuilder.TopLimit));
        Assert.That(result.TopCustomers.ByOrders.Items[0].Key, Is.EqualTo("customer:12"));
        Assert.That(result.TopCustomers.ByOrders.Items, Has.Count.EqualTo(CustomersReportBuilder.TopLimit));
    }

    [Test]
    public void Build_ActiveCustomers_PointInTimeAtPeriodEnds_FromChanges()
    {
        var data = CustomersReportData.Empty with
        {
            ActiveChanges =
            [
                new(new(2026, 6, 1), 5),     // before the range: part of the count on Aug 31
                new(new(2026, 8, 20), -1),
                new(new(2026, 9, 3), 2),
                new(new(2026, 9, 10), -3),
                new(new(2026, 10, 5), -3),   // after the range, ignored
            ],
        };

        var daily = CustomersReportBuilder.Build(query, data, statuses);
        var weekly = CustomersReportBuilder.Build(query with { Range = range with { Grouping = StatsGrouping.Week } }, data, statuses);

        Assert.That(daily.ActiveCustomers.Values[0], Is.EqualTo(4m));
        Assert.That(daily.ActiveCustomers.Values[2], Is.EqualTo(6m));
        Assert.That(daily.ActiveCustomers.Values[9], Is.EqualTo(3m));
        Assert.That(daily.ActiveCustomers.Total, Is.EqualTo(3m));
        // Weeks start Mon Aug 31: the first week ends Sep 6 (6 active), the second Sep 13 (3), the last is the range end.
        Assert.That(weekly.ActiveCustomers.Values, Is.EqualTo(new[] { 6m, 3m, 3m, 3m, 3m }));
        Assert.That(daily.Totals.ActiveCustomers.Current, Is.EqualTo(3m));
        Assert.That(daily.Totals.ActiveCustomers.Previous, Is.EqualTo(4m));
        Assert.That(daily.Totals.ActiveCustomers.Change, Is.EqualTo(-0.25).Within(1e-9));
    }

    [Test]
    public void Build_ActiveCustomers_NoOrders_Zeros_AndWindowKept()
    {
        var result = CustomersReportBuilder.Build(query with { ActivityWindowDays = 30 }, CustomersReportData.Empty, statuses);

        Assert.That(result.ActiveCustomers.Values, Has.Count.EqualTo(30).And.All.Zero);
        Assert.That(result.Totals.ActiveCustomers.Current, Is.Zero);
        Assert.That(result.Totals.ActiveCustomers.Change, Is.Null);
        Assert.That(result.ActivityWindowDays, Is.EqualTo(30));
    }

    [TestCase("Ann", "Lee", "a@example.com", "Ann Lee")]
    [TestCase(" Ann ", null, "a@example.com", "Ann")]
    [TestCase(null, "Lee", null, "Lee")]
    [TestCase("", " ", " a@example.com ", "a@example.com")]
    [TestCase(null, null, null, "Customer #7")]
    public void GetCustomerName_FallsBackToEmailThenId(string? first, string? last, string? email, string expected) =>
        Assert.That(CustomersReportBuilder.GetCustomerName(7, first, last, email), Is.EqualTo(expected));

    [Test]
    public void Build_KeepsFilters_AndGrouping()
    {
        var result = CustomersReportBuilder.Build(
            query with { Range = range with { Grouping = StatsGrouping.Week }, OrderStatusId = 4, AddressType = CustomersAddressType.Shipping },
            CustomersReportData.Empty,
            statuses);

        Assert.That(result.OrderStatusId, Is.EqualTo(4));
        Assert.That(result.AddressType, Is.EqualTo(CustomersAddressType.Shipping));
        Assert.That(result.Grouping, Is.EqualTo(StatsGrouping.Week));
        Assert.That(result.Periods, Has.Count.EqualTo(result.TotalCustomers.Values.Count));
    }
}
