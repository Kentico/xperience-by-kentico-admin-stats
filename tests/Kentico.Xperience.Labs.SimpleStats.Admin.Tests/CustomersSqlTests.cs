using CMS.Commerce;
using CMS.Globalization;

using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Commerce;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Customers;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class CustomersSqlTests
{
    private static readonly DateOnly today = new(2026, 9, 30);

    [Test]
    public void BuildReport_ChecksTablesFirst_AndReturnsEarly()
    {
        string sql = CustomersSql.BuildReport(filterByStatus: false);

        string[] tables = ["Commerce_Customer", "Commerce_Order", "Commerce_OrderItem", "Commerce_OrderAddress", "CMS_Country", "CMS_State"];
        Assert.That(tables, Is.All.Matches<string>(table => sql.Contains($"OBJECT_ID(N'[{table}]', N'U') IS NULL", StringComparison.Ordinal)));
        Assert.That(sql.IndexOf("RETURN;", StringComparison.Ordinal), Is.LessThan(sql.IndexOf("FROM [Commerce_Order] O", StringComparison.Ordinal)));
    }

    [Test]
    public void BuildReport_WithoutStatus_HasNoStatusParameter() =>
        Assert.That(CustomersSql.BuildReport(filterByStatus: false), Does.Not.Contain(CommerceSql.OrderStatusIdParameter));

    [Test]
    public void BuildReport_WithStatus_FiltersOrders_NotNewCustomers()
    {
        string sql = CustomersSql.BuildReport(filterByStatus: true);

        // Orders in the periods, the two "order before the period" checks of returning customers, and the active customers orders.
        Assert.That(CountOf(sql, "AND O.[OrderOrderStatusID] = @OrderStatusId"), Is.EqualTo(4));

        int daily = sql.IndexOf("CAST(C.[CustomerCreatedWhen] AS date) AS [CreatedDate]", StringComparison.Ordinal);
        int nextStatement = sql.IndexOf(';', daily);
        Assert.That(sql[daily..nextStatement], Does.Not.Contain("@OrderStatusId"));
    }

    [Test]
    public void BuildReport_UsesParameters_AndCaseInsensitiveAddressType()
    {
        string sql = CustomersSql.BuildReport(filterByStatus: true);

        Assert.That(sql, Does.Contain("@PreviousFrom").And.Contain("@From").And.Contain("@ToExclusive"));
        Assert.That(sql, Does.Contain("TOP (@Limit)").And.Contain("TOP (@CountryLimit)"));
        Assert.That(sql, Does.Contain("LOWER(A.[OrderAddressType]) = LOWER(@AddressType)"));
        Assert.That(sql, Does.Contain("SUM(ISNULL(I.[OrderItemQuantity], 0))"));
        Assert.That(sql, Does.Contain("ISNULL(O.[OrderGrandTotal], 0)"));
        // The most recent order of each period decides the location.
        Assert.That(sql, Does.Contain("ORDER BY O.[OrderCreatedWhen] DESC, O.[OrderID] DESC"));
        // Customers without the address (or country) stay as the NULL country.
        Assert.That(sql, Does.Contain("LEFT JOIN [CMS_Country] CT ON CT.[CountryID] = L.[CountryID]"));
    }

    /// <summary>
    /// The SQL reads these columns; the product's Info classes must still have them.
    /// </summary>
    [Test]
    public void Sql_UsesColumnsOfTheInfoClasses()
    {
        string sql = CustomersSql.BuildReport(filterByStatus: true);

        string[] columns =
        [
            nameof(CustomerInfo.CustomerID),
            nameof(CustomerInfo.CustomerCreatedWhen),
            nameof(CustomerInfo.CustomerFirstName),
            nameof(CustomerInfo.CustomerLastName),
            nameof(CustomerInfo.CustomerEmail),
            nameof(OrderInfo.OrderID),
            nameof(OrderInfo.OrderCustomerID),
            nameof(OrderInfo.OrderCreatedWhen),
            nameof(OrderInfo.OrderOrderStatusID),
            nameof(OrderInfo.OrderGrandTotal),
            nameof(OrderItemInfo.OrderItemOrderID),
            nameof(OrderItemInfo.OrderItemQuantity),
            nameof(OrderAddressInfo.OrderAddressID),
            nameof(OrderAddressInfo.OrderAddressOrderID),
            nameof(OrderAddressInfo.OrderAddressType),
            nameof(OrderAddressInfo.OrderAddressCountryID),
            nameof(OrderAddressInfo.OrderAddressStateID),
            nameof(CountryInfo.CountryID),
            nameof(CountryInfo.CountryDisplayName),
            nameof(StateInfo.StateID),
            nameof(StateInfo.StateDisplayName),
        ];

        Assert.That(columns, Is.All.Matches<string>(column => sql.Contains($"[{column}]", StringComparison.Ordinal)));
    }

    [Test]
    public void BuildReport_ActiveChanges_WindowEdges()
    {
        string sql = CustomersSql.BuildReport(filterByStatus: false);

        // Orders on days (ActiveFrom - window, ...]: an order exactly window days before a day does not make the customer active that day.
        Assert.That(sql, Does.Contain("O.[OrderCreatedWhen] >= DATEADD(day, 1 - @ActivityWindow, @ActiveFrom)"));
        // A spell continues while the previous order day is less than window days earlier, and ends window days after its last order day.
        Assert.That(sql, Does.Contain("> DATEADD(day, -@ActivityWindow, D.[OrderDay])"));
        Assert.That(sql, Does.Contain("DATEADD(day, @ActivityWindow, MAX(P.[OrderDay])) AS [EndDay]"));
    }

    [TestCase(30, 30)]
    [TestCase(180, 180)]
    [TestCase(45, 90)]
    [TestCase(0, 90)]
    [TestCase(null, 90)]
    public void Filter_Normalize_ActivityWindow_AllowedOrDefault(int? value, int expected) =>
        Assert.That(new CustomersFilter { ActivityWindowDays = value }.Normalize(today).ActivityWindowDays, Is.EqualTo(expected));

    [Test]
    public void AddressTypeName_UsesProductConstants()
    {
        Assert.That(CustomersRepository.GetAddressTypeName(CustomersAddressType.Billing), Is.EqualTo(OrderAddressType.Billing.Name));
        Assert.That(CustomersRepository.GetAddressTypeName(CustomersAddressType.Shipping), Is.EqualTo(OrderAddressType.Shipping.Name));
    }

    [Test]
    public void AvailabilityCheck_OneConditionPerTable()
    {
        string sql = CommerceSql.BuildAvailabilityCheck("A", "B");

        Assert.That(sql, Does.StartWith("IF OBJECT_ID(N'[A]', N'U') IS NULL"));
        Assert.That(sql, Does.Contain("OR OBJECT_ID(N'[B]', N'U') IS NULL"));
        Assert.That(sql, Does.Contain("SELECT CAST(0 AS bit) AS [CommerceAvailable];"));
    }

    [Test]
    public void Filter_Normalize_Defaults_AllStatusesBillingLast30Days()
    {
        var query = new CustomersFilter().Normalize(today);

        Assert.That(query.OrderStatusId, Is.Null);
        Assert.That(query.AddressType, Is.EqualTo(CustomersAddressType.Billing));
        Assert.That(query.Range, Is.EqualTo(new StatsFilter().Normalize(today)));
    }

    [TestCase(CustomersAddressType.Shipping, CustomersAddressType.Shipping)]
    [TestCase(CustomersAddressType.Billing, CustomersAddressType.Billing)]
    [TestCase((CustomersAddressType)42, CustomersAddressType.Billing)]
    [TestCase(null, CustomersAddressType.Billing)]
    public void Filter_Normalize_UnknownAddressTypeIsBilling(CustomersAddressType? value, CustomersAddressType expected) =>
        Assert.That(new CustomersFilter { AddressType = value }.Normalize(today).AddressType, Is.EqualTo(expected));

    [TestCase(3, 3)]
    [TestCase(0, null)]
    [TestCase(-1, null)]
    [TestCase(null, null)]
    public void Filter_Normalize_StatusIdPositiveOnly(int? value, int? expected) =>
        Assert.That(new CustomersFilter { OrderStatusId = value }.Normalize(today).OrderStatusId, Is.EqualTo(expected));

    [Test]
    public void Filter_Normalize_DropsChannel_KeepsRangeAndGrouping()
    {
        var filter = new CustomersFilter
        {
            Range = new StatsFilter { From = new(2026, 9, 1), To = new(2026, 9, 10), Grouping = StatsGrouping.Week, ChannelId = 3 },
        };

        Assert.That(filter.Normalize(today).Range, Is.EqualTo(new StatsQuery(new(2026, 9, 1), new(2026, 9, 10), StatsGrouping.Week, null)));
    }

    [Test]
    public void AddressType_SerializesAsString() =>
        Assert.That(System.Text.Json.JsonSerializer.Serialize(CustomersAddressType.Shipping), Is.EqualTo("\"Shipping\""));

    private static int CountOf(string text, string value)
    {
        int count = 0;
        for (int index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
