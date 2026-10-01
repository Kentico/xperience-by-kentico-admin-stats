using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Commerce;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Customers;

/// <summary>
/// Builds the customers batch. Only constant SQL fragments are combined; all values are parameters.
/// Tables and columns are those of <c>CMS.Commerce.CustomerInfo</c> (<c>Commerce_Customer</c>), <c>OrderInfo</c> (<c>Commerce_Order</c>),
/// <c>OrderItemInfo</c> (<c>Commerce_OrderItem</c>), <c>OrderAddressInfo</c> (<c>Commerce_OrderAddress</c>)
/// and <c>CMS.Globalization.CountryInfo</c> / <c>StateInfo</c> (<c>CMS_Country</c>, <c>CMS_State</c>).
/// </summary>
/// <remarks>
/// The batch starts with one row (<see cref="CommerceSql.AvailableColumn"/>): <c>0</c> when a table does not exist, and then returns nothing else.
/// It first aggregates the orders of the previous period + range per customer (status filter applied) into a table variable,
/// with the most recent order of each period, and then returns, in order:
/// <list type="number">
/// <item>Customers created per day from the start of the previous period to the end of the range (no status filter).</item>
/// <item>Totals, one row: customers created before the range, ordering and returning customers, revenue, orders and item quantity.</item>
/// <item>Top countries by ordering customers, with the previous period count. Location = the address of <see cref="AddressTypeParameter"/>
/// on the customer's most recent order of the period; no address or no country is the <c>NULL</c> country.</item>
/// <item>Top states (customers without a state are left out), with the previous period count.</item>
/// <item>Ordering customers in the top <see cref="LimitParameter"/> by revenue, orders or item quantity, with their positions.</item>
/// <item>Changes of the active customers count per day (status filter applied), see <see cref="ActiveChangesQuery"/>.</item>
/// </list>
/// <c>Commerce_Order</c> has indexes on <c>OrderCreatedWhen</c> and <c>OrderCustomerID</c>, and <c>Commerce_OrderItem</c> / <c>Commerce_OrderAddress</c>
/// on their order ID, so the range, returning customer and per-order lookups can seek.
/// </remarks>
internal static class CustomersSql
{
    /// <summary>Start of the previous period (inclusive).</summary>
    public const string PreviousFromParameter = "@PreviousFrom";

    /// <summary>Start of the range (inclusive). Earlier orders belong to the previous period.</summary>
    public const string FromParameter = "@From";

    /// <summary>Day after the end of the range (exclusive).</summary>
    public const string ToExclusiveParameter = "@ToExclusive";

    /// <summary>Order address type name (<c>OrderAddressType.Name</c>) of the location tiles. Compared case-insensitively.</summary>
    public const string AddressTypeParameter = "@AddressType";

    /// <summary>Maximum number of top states and top customers per list.</summary>
    public const string LimitParameter = "@Limit";

    /// <summary>Maximum number of top countries.</summary>
    public const string CountryLimitParameter = "@CountryLimit";

    /// <summary>First day with an active customers count (the last day of the previous period).</summary>
    public const string ActiveFromParameter = "@ActiveFrom";

    /// <summary>Activity window in days: a customer is active on day D with an order on a day in (D - window, D].</summary>
    public const string ActivityWindowParameter = "@ActivityWindow";

    public const string DateColumn = "CreatedDate";
    public const string CustomersColumn = "CustomerCount";
    public const string PreviousCustomersColumn = "PreviousCustomerCount";
    public const string CustomersBeforeColumn = "CustomersBefore";
    public const string OrderingColumn = "OrderingCustomers";
    public const string PreviousOrderingColumn = "PreviousOrderingCustomers";
    public const string ReturningColumn = "ReturningCustomers";
    public const string PreviousReturningColumn = "PreviousReturningCustomers";
    public const string RevenueColumn = "Revenue";
    public const string PreviousRevenueColumn = "PreviousRevenue";
    public const string OrdersColumn = "OrderCount";
    public const string PreviousOrdersColumn = "PreviousOrderCount";
    public const string QuantityColumn = "Quantity";
    public const string PreviousQuantityColumn = "PreviousQuantity";
    public const string CountryIdColumn = "CountryID";
    public const string CountryNameColumn = "CountryDisplayName";
    public const string StateIdColumn = "StateID";
    public const string StateNameColumn = "StateDisplayName";
    public const string GroupCountColumn = "GroupCount";
    public const string TotalCustomersColumn = "TotalCustomers";
    public const string CustomerIdColumn = "CustomerID";
    public const string FirstNameColumn = "CustomerFirstName";
    public const string LastNameColumn = "CustomerLastName";
    public const string EmailColumn = "CustomerEmail";
    public const string RevenueRankColumn = "RevenueRank";
    public const string OrdersRankColumn = "OrdersRank";
    public const string QuantityRankColumn = "QuantityRank";
    public const string ChangeDateColumn = "ChangeDate";
    public const string ChangeColumn = "ActiveChange";

    private static readonly string availabilityCheck = CommerceSql.BuildAvailabilityCheck(
        "Commerce_Customer",
        "Commerce_Order",
        "Commerce_OrderItem",
        "Commerce_OrderAddress",
        "CMS_Country",
        "CMS_State");

    // Orders of the previous period + range per customer ({0} = status condition). Recency 1 = the most recent order of its period.
    private const string CustomerOrdersQuery = """
        DECLARE @CustomerOrders TABLE (
            [CustomerID] int NOT NULL PRIMARY KEY,
            [CurrentOrders] int NOT NULL,
            [PreviousOrders] int NOT NULL,
            [CurrentRevenue] decimal(38, 4) NOT NULL,
            [PreviousRevenue] decimal(38, 4) NOT NULL,
            [CurrentQuantity] decimal(38, 4) NOT NULL,
            [PreviousQuantity] decimal(38, 4) NOT NULL,
            [CurrentLastOrderID] int NULL,
            [PreviousLastOrderID] int NULL);

        INSERT INTO @CustomerOrders
        SELECT
            O2.[OrderCustomerID],
            SUM(O2.[IsCurrent]),
            SUM(1 - O2.[IsCurrent]),
            SUM(CASE WHEN O2.[IsCurrent] = 1 THEN O2.[Revenue] ELSE 0 END),
            SUM(CASE WHEN O2.[IsCurrent] = 0 THEN O2.[Revenue] ELSE 0 END),
            SUM(CASE WHEN O2.[IsCurrent] = 1 THEN O2.[Quantity] ELSE 0 END),
            SUM(CASE WHEN O2.[IsCurrent] = 0 THEN O2.[Quantity] ELSE 0 END),
            MAX(CASE WHEN O2.[IsCurrent] = 1 AND O2.[Recency] = 1 THEN O2.[OrderID] ELSE 0 END),
            MAX(CASE WHEN O2.[IsCurrent] = 0 AND O2.[Recency] = 1 THEN O2.[OrderID] ELSE 0 END)
        FROM (
            SELECT
                O.[OrderID],
                O.[OrderCustomerID],
                O1.[IsCurrent],
                ISNULL(O.[OrderGrandTotal], 0) AS [Revenue],
                ISNULL(Q.[Quantity], 0) AS [Quantity],
                ROW_NUMBER() OVER (PARTITION BY O.[OrderCustomerID], O1.[IsCurrent] ORDER BY O.[OrderCreatedWhen] DESC, O.[OrderID] DESC) AS [Recency]
            FROM [Commerce_Order] O
            CROSS APPLY (SELECT CASE WHEN O.[OrderCreatedWhen] >= @From THEN 1 ELSE 0 END AS [IsCurrent]) O1
            OUTER APPLY (
                SELECT SUM(ISNULL(I.[OrderItemQuantity], 0)) AS [Quantity]
                FROM [Commerce_OrderItem] I
                WHERE I.[OrderItemOrderID] = O.[OrderID]
            ) Q
            WHERE O.[OrderCreatedWhen] >= @PreviousFrom
                AND O.[OrderCreatedWhen] < @ToExclusive{0}
        ) O2
        GROUP BY O2.[OrderCustomerID];

        -- Order IDs are positive; 0 above means "no order in that period" (avoids NULLs in aggregates).
        UPDATE @CustomerOrders SET [CurrentLastOrderID] = NULLIF([CurrentLastOrderID], 0), [PreviousLastOrderID] = NULLIF([PreviousLastOrderID], 0);

        -- Location of each ordering customer per period: the chosen address on the most recent order (no address = NULL country).
        DECLARE @Locations TABLE ([IsCurrent] int NOT NULL, [CountryID] int NULL, [StateID] int NULL);

        INSERT INTO @Locations ([IsCurrent], [CountryID], [StateID])
        SELECT P.[IsCurrent], A.[OrderAddressCountryID], A.[OrderAddressStateID]
        FROM (
            SELECT 1 AS [IsCurrent], CO.[CurrentLastOrderID] AS [OrderID] FROM @CustomerOrders CO WHERE CO.[CurrentLastOrderID] IS NOT NULL
            UNION ALL
            SELECT 0, CO.[PreviousLastOrderID] FROM @CustomerOrders CO WHERE CO.[PreviousLastOrderID] IS NOT NULL
        ) P
        OUTER APPLY (
            SELECT TOP (1) A.[OrderAddressCountryID], A.[OrderAddressStateID]
            FROM [Commerce_OrderAddress] A
            WHERE A.[OrderAddressOrderID] = P.[OrderID]
                AND LOWER(A.[OrderAddressType]) = LOWER(@AddressType)
            ORDER BY A.[OrderAddressID]
        ) A;
        """;

    // 1. Customers created per day, previous period + range. The status filter does not apply (a customer can exist without orders).
    private const string DailyQuery = """
        SELECT
            CAST(C.[CustomerCreatedWhen] AS date) AS [CreatedDate],
            COUNT(*) AS [CustomerCount]
        FROM [Commerce_Customer] C
        WHERE C.[CustomerCreatedWhen] >= @PreviousFrom
            AND C.[CustomerCreatedWhen] < @ToExclusive
        GROUP BY CAST(C.[CustomerCreatedWhen] AS date);
        """;

    // 2. Totals, one row ({0} = status condition for the "order before the period" checks).
    private const string TotalsQuery = """
        SELECT
            (SELECT COUNT(*) FROM [Commerce_Customer] C WHERE C.[CustomerCreatedWhen] < @From) AS [CustomersBefore],
            ISNULL(SUM(R.[IsOrdering]), 0) AS [OrderingCustomers],
            ISNULL(SUM(R.[IsPreviousOrdering]), 0) AS [PreviousOrderingCustomers],
            ISNULL(SUM(R.[IsReturning]), 0) AS [ReturningCustomers],
            ISNULL(SUM(R.[IsPreviousReturning]), 0) AS [PreviousReturningCustomers],
            ISNULL(SUM(R.[CurrentRevenue]), 0) AS [Revenue],
            ISNULL(SUM(R.[PreviousRevenue]), 0) AS [PreviousRevenue],
            ISNULL(SUM(R.[CurrentOrders]), 0) AS [OrderCount],
            ISNULL(SUM(R.[CurrentQuantity]), 0) AS [Quantity]
        FROM (
            SELECT
                CO.*,
                CASE WHEN CO.[CurrentOrders] > 0 THEN 1 ELSE 0 END AS [IsOrdering],
                CASE WHEN CO.[PreviousOrders] > 0 THEN 1 ELSE 0 END AS [IsPreviousOrdering],
                CASE WHEN CO.[CurrentOrders] > 0 AND EXISTS (
                    SELECT 1 FROM [Commerce_Order] O
                    WHERE O.[OrderCustomerID] = CO.[CustomerID]
                        AND O.[OrderCreatedWhen] < @From{0}) THEN 1 ELSE 0 END AS [IsReturning],
                CASE WHEN CO.[PreviousOrders] > 0 AND EXISTS (
                    SELECT 1 FROM [Commerce_Order] O
                    WHERE O.[OrderCustomerID] = CO.[CustomerID]
                        AND O.[OrderCreatedWhen] < @PreviousFrom{0}) THEN 1 ELSE 0 END AS [IsPreviousReturning]
            FROM @CustomerOrders CO
        ) R;
        """;

    // 3. Top countries. The NULL country is customers without the address or without a country ("Unknown").
    private const string CountriesQuery = """
        SELECT TOP (@CountryLimit)
            L.[CountryID],
            MAX(CT.[CountryDisplayName]) AS [CountryDisplayName],
            SUM(L.[IsCurrent]) AS [CustomerCount],
            SUM(1 - L.[IsCurrent]) AS [PreviousCustomerCount],
            COUNT(*) OVER () AS [GroupCount]
        FROM @Locations L
        LEFT JOIN [CMS_Country] CT ON CT.[CountryID] = L.[CountryID]
        GROUP BY L.[CountryID]
        HAVING SUM(L.[IsCurrent]) > 0
        ORDER BY [CustomerCount] DESC, L.[CountryID];
        """;

    // 4. Top states with their country. COUNT(*) OVER () and SUM(...) OVER () cover every state with customers (before TOP).
    private const string StatesQuery = """
        SELECT TOP (@Limit)
            S.[StateID],
            MAX(S.[StateDisplayName]) AS [StateDisplayName],
            MAX(CT.[CountryDisplayName]) AS [CountryDisplayName],
            SUM(L.[IsCurrent]) AS [CustomerCount],
            SUM(1 - L.[IsCurrent]) AS [PreviousCustomerCount],
            COUNT(*) OVER () AS [GroupCount],
            SUM(SUM(L.[IsCurrent])) OVER () AS [TotalCustomers]
        FROM @Locations L
        INNER JOIN [CMS_State] S ON S.[StateID] = L.[StateID]
        LEFT JOIN [CMS_Country] CT ON CT.[CountryID] = S.[CountryID]
        GROUP BY S.[StateID]
        HAVING SUM(L.[IsCurrent]) > 0
        ORDER BY [CustomerCount] DESC, S.[StateID];
        """;

    // 5. Ordering customers in any of the three top lists, with their positions (ties by customer ID).
    private const string TopCustomersQuery = """
        SELECT R.*
        FROM (
            SELECT
                CO.[CustomerID],
                C.[CustomerFirstName],
                C.[CustomerLastName],
                C.[CustomerEmail],
                CO.[CurrentRevenue] AS [Revenue],
                CO.[CurrentOrders] AS [OrderCount],
                CO.[CurrentQuantity] AS [Quantity],
                CO.[PreviousRevenue],
                CO.[PreviousOrders] AS [PreviousOrderCount],
                CO.[PreviousQuantity],
                ROW_NUMBER() OVER (ORDER BY CO.[CurrentRevenue] DESC, CO.[CustomerID]) AS [RevenueRank],
                ROW_NUMBER() OVER (ORDER BY CO.[CurrentOrders] DESC, CO.[CustomerID]) AS [OrdersRank],
                ROW_NUMBER() OVER (ORDER BY CO.[CurrentQuantity] DESC, CO.[CustomerID]) AS [QuantityRank]
            FROM @CustomerOrders CO
            INNER JOIN [Commerce_Customer] C ON C.[CustomerID] = CO.[CustomerID]
            WHERE CO.[CurrentOrders] > 0
        ) R
        WHERE R.[RevenueRank] <= @Limit OR R.[OrdersRank] <= @Limit OR R.[QuantityRank] <= @Limit;
        """;

    /// <summary>
    /// 6. Changes of the active customers count per day ({0} = status condition). A customer is active on day D when they have an order
    /// on a day in (D - window, D]. Order days of each customer form "active spells": a new spell starts when the previous order day is
    /// window or more days earlier; a spell lasts from its first order day to its last order day + window (exclusive).
    /// Each spell is +1 on its start and -1 on its end, so the running sum of the changes is the active count per day, grouping-independent.
    /// Only orders that can make a customer active on <see cref="ActiveFromParameter"/> or later are read.
    /// </summary>
    private const string ActiveChangesQuery = """
        WITH [Days] AS (
            SELECT DISTINCT O.[OrderCustomerID] AS [CustomerID], CAST(O.[OrderCreatedWhen] AS date) AS [OrderDay]
            FROM [Commerce_Order] O
            WHERE O.[OrderCreatedWhen] >= DATEADD(day, 1 - @ActivityWindow, @ActiveFrom)
                AND O.[OrderCreatedWhen] < @ToExclusive{0}
        ), [Starts] AS (
            SELECT
                D.[CustomerID],
                D.[OrderDay],
                CASE WHEN LAG(D.[OrderDay]) OVER (PARTITION BY D.[CustomerID] ORDER BY D.[OrderDay]) > DATEADD(day, -@ActivityWindow, D.[OrderDay])
                    THEN 0 ELSE 1 END AS [IsStart]
            FROM [Days] D
        ), [Spells] AS (
            SELECT
                S.[CustomerID],
                S.[OrderDay],
                SUM(S.[IsStart]) OVER (PARTITION BY S.[CustomerID] ORDER BY S.[OrderDay] ROWS UNBOUNDED PRECEDING) AS [Spell]
            FROM [Starts] S
        ), [Ranges] AS (
            SELECT MIN(P.[OrderDay]) AS [StartDay], DATEADD(day, @ActivityWindow, MAX(P.[OrderDay])) AS [EndDay]
            FROM [Spells] P
            GROUP BY P.[CustomerID], P.[Spell]
        )
        SELECT C.[ChangeDate], SUM(C.[Change]) AS [ActiveChange]
        FROM (
            SELECT R.[StartDay] AS [ChangeDate], 1 AS [Change] FROM [Ranges] R
            UNION ALL
            SELECT R.[EndDay], -1 FROM [Ranges] R
        ) C
        GROUP BY C.[ChangeDate]
        HAVING SUM(C.[Change]) <> 0;
        """;

    /// <summary>
    /// Builds the report batch. With <paramref name="filterByStatus"/>, order-based results read only orders in
    /// <see cref="CommerceSql.OrderStatusIdParameter"/> (new customers are not filtered).
    /// </summary>
    public static string BuildReport(bool filterByStatus)
    {
        string status = filterByStatus ? CommerceSql.StatusCondition : string.Empty;

        return string.Join(
            Environment.NewLine,
            "SET NOCOUNT ON;",
            availabilityCheck,
            string.Format(CustomerOrdersQuery, status),
            DailyQuery,
            string.Format(TotalsQuery, status),
            CountriesQuery,
            StatesQuery,
            TopCustomersQuery,
            string.Format(ActiveChangesQuery, status));
    }
}
