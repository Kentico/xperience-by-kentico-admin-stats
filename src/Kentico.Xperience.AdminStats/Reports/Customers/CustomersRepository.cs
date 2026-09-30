using System.Data;
using System.Data.Common;

using CMS.Commerce;
using CMS.DataEngine;

using Kentico.Xperience.AdminStats.Reports.Commerce;

namespace Kentico.Xperience.AdminStats.Reports.Customers;

/// <summary>
/// Reads aggregated customer data from the database.
/// </summary>
internal interface ICustomersRepository
{
    /// <summary>
    /// Returns customers created per day from <paramref name="previousFrom"/> to <paramref name="to"/>, totals, top locations
    /// and top customers (with <paramref name="orderStatusId"/> applied to orders). <see cref="CustomersReportData.Unavailable"/>
    /// when the commerce tables do not exist.
    /// </summary>
    /// <param name="previousFrom">First day of the previous period (inclusive).</param>
    /// <param name="from">First day of the range (inclusive).</param>
    /// <param name="to">Last day of the range (inclusive).</param>
    /// <param name="orderStatusId">Optional order status ID.</param>
    /// <param name="addressType">Address of the location results.</param>
    /// <param name="limit">Maximum number of top states and top customers per list.</param>
    /// <param name="countryLimit">Maximum number of top countries.</param>
    /// <param name="activityWindowDays">Activity window (days) of the active customers changes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<CustomersReportData> GetData(
        DateOnly previousFrom,
        DateOnly from,
        DateOnly to,
        int? orderStatusId,
        CustomersAddressType addressType,
        int limit,
        int countryLimit,
        int activityWindowDays,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the order statuses in status order. Empty when the commerce tables do not exist.
    /// </summary>
    public Task<IReadOnlyList<CommerceOrderStatusOption>> GetStatuses(CancellationToken cancellationToken);
}

internal sealed class CustomersRepository : ICustomersRepository
{
    private const string ReportName = "customers";

    public async Task<CustomersReportData> GetData(
        DateOnly previousFrom,
        DateOnly from,
        DateOnly to,
        int? orderStatusId,
        CustomersAddressType addressType,
        int limit,
        int countryLimit,
        int activityWindowDays,
        CancellationToken cancellationToken)
    {
        var parameters = new QueryDataParameters
        {
            new DataParameter(CustomersSql.PreviousFromParameter, previousFrom.ToDateTime(TimeOnly.MinValue)),
            new DataParameter(CustomersSql.FromParameter, from.ToDateTime(TimeOnly.MinValue)),
            new DataParameter(CustomersSql.ToExclusiveParameter, to.AddDays(1).ToDateTime(TimeOnly.MinValue)),
            new DataParameter(CustomersSql.AddressTypeParameter, GetAddressTypeName(addressType)),
            new DataParameter(CustomersSql.LimitParameter, limit),
            new DataParameter(CustomersSql.CountryLimitParameter, countryLimit),
            // The active count starts on the last day of the previous period (the KPI's previous value).
            new DataParameter(CustomersSql.ActiveFromParameter, from.AddDays(-1).ToDateTime(TimeOnly.MinValue)),
            new DataParameter(CustomersSql.ActivityWindowParameter, activityWindowDays),
        };
        if (orderStatusId is int statusId)
        {
            parameters.Add(new DataParameter(CommerceSql.OrderStatusIdParameter, statusId));
        }

        string sql = CustomersSql.BuildReport(orderStatusId is not null);

        await using var reader = await ConnectionHelper.ExecuteReaderAsync(sql, parameters, QueryTypeEnum.SQLQuery, CommandBehavior.Default, cancellationToken);

        if (!await CommerceSql.IsAvailable(reader, cancellationToken))
        {
            return CustomersReportData.Unavailable;
        }

        await CommerceSql.NextResult(reader, ReportName, cancellationToken);
        var daily = await ReadDaily(reader, cancellationToken);
        await CommerceSql.NextResult(reader, ReportName, cancellationToken);
        var totals = await ReadTotals(reader, cancellationToken);
        await CommerceSql.NextResult(reader, ReportName, cancellationToken);
        var (countries, countryCount, _) = await ReadLocations(reader, withCountry: false, cancellationToken);
        await CommerceSql.NextResult(reader, ReportName, cancellationToken);
        var (states, stateCount, stateCustomers) = await ReadLocations(reader, withCountry: true, cancellationToken);
        await CommerceSql.NextResult(reader, ReportName, cancellationToken);
        var customers = await ReadCustomers(reader, cancellationToken);
        await CommerceSql.NextResult(reader, ReportName, cancellationToken);
        var activeChanges = await ReadActiveChanges(reader, cancellationToken);

        return new(true, daily, totals, countries, countryCount, states, stateCount, stateCustomers, customers)
        {
            ActiveChanges = activeChanges,
        };
    }

    public Task<IReadOnlyList<CommerceOrderStatusOption>> GetStatuses(CancellationToken cancellationToken) =>
        CommerceOrderStatuses.Read(cancellationToken);

    /// <summary>
    /// Stored name of the address type (the product's <see cref="OrderAddressType"/> constants).
    /// </summary>
    internal static string GetAddressTypeName(CustomersAddressType addressType) =>
        addressType == CustomersAddressType.Shipping ? OrderAddressType.Shipping.Name : OrderAddressType.Billing.Name;

    private static async Task<IReadOnlyList<CustomersDailyRow>> ReadDaily(DbDataReader reader, CancellationToken cancellationToken)
    {
        int dateOrdinal = reader.GetOrdinal(CustomersSql.DateColumn);
        int customersOrdinal = reader.GetOrdinal(CustomersSql.CustomersColumn);

        var rows = new List<CustomersDailyRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(DateOnly.FromDateTime(reader.GetDateTime(dateOrdinal)), reader.GetInt32(customersOrdinal)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CustomersActiveChangeRow>> ReadActiveChanges(DbDataReader reader, CancellationToken cancellationToken)
    {
        int dateOrdinal = reader.GetOrdinal(CustomersSql.ChangeDateColumn);
        int changeOrdinal = reader.GetOrdinal(CustomersSql.ChangeColumn);

        var rows = new List<CustomersActiveChangeRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(DateOnly.FromDateTime(reader.GetDateTime(dateOrdinal)), reader.GetInt32(changeOrdinal)));
        }

        return rows;
    }

    private static async Task<CustomersTotalsRow> ReadTotals(DbDataReader reader, CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken))
        {
            return CustomersTotalsRow.Empty;
        }

        int Int(string column) => reader.GetInt32(reader.GetOrdinal(column));
        decimal Decimal(string column) => reader.GetDecimal(reader.GetOrdinal(column));

        return new(
            Int(CustomersSql.CustomersBeforeColumn),
            Int(CustomersSql.OrderingColumn),
            Int(CustomersSql.PreviousOrderingColumn),
            Int(CustomersSql.ReturningColumn),
            Int(CustomersSql.PreviousReturningColumn),
            Decimal(CustomersSql.RevenueColumn),
            Decimal(CustomersSql.PreviousRevenueColumn),
            Int(CustomersSql.OrdersColumn),
            Decimal(CustomersSql.QuantityColumn));
    }

    /// <summary>
    /// Reads countries (<paramref name="withCountry"/> <c>false</c>) or states with their country name.
    /// Returns the rows, the number of groups and, for states, the customers of all groups.
    /// </summary>
    private static async Task<(IReadOnlyList<CustomersLocationRow> Rows, int GroupCount, int TotalCustomers)> ReadLocations(
        DbDataReader reader,
        bool withCountry,
        CancellationToken cancellationToken)
    {
        int idOrdinal = reader.GetOrdinal(withCountry ? CustomersSql.StateIdColumn : CustomersSql.CountryIdColumn);
        int nameOrdinal = reader.GetOrdinal(withCountry ? CustomersSql.StateNameColumn : CustomersSql.CountryNameColumn);
        int countryOrdinal = withCountry ? reader.GetOrdinal(CustomersSql.CountryNameColumn) : -1;
        int customersOrdinal = reader.GetOrdinal(CustomersSql.CustomersColumn);
        int previousOrdinal = reader.GetOrdinal(CustomersSql.PreviousCustomersColumn);
        int groupCountOrdinal = reader.GetOrdinal(CustomersSql.GroupCountColumn);
        int totalOrdinal = withCountry ? reader.GetOrdinal(CustomersSql.TotalCustomersColumn) : -1;

        string? GetString(int ordinal) => ordinal < 0 || reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

        var rows = new List<CustomersLocationRow>();
        int groupCount = 0;
        int totalCustomers = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.IsDBNull(idOrdinal) ? null : reader.GetInt32(idOrdinal),
                GetString(nameOrdinal),
                GetString(countryOrdinal),
                reader.GetInt32(customersOrdinal),
                reader.GetInt32(previousOrdinal)));

            // Same values on every row.
            groupCount = reader.GetInt32(groupCountOrdinal);
            totalCustomers = totalOrdinal < 0 ? 0 : reader.GetInt32(totalOrdinal);
        }

        return (rows, groupCount, totalCustomers);
    }

    private static async Task<IReadOnlyList<CustomersCustomerRow>> ReadCustomers(DbDataReader reader, CancellationToken cancellationToken)
    {
        int idOrdinal = reader.GetOrdinal(CustomersSql.CustomerIdColumn);
        int firstNameOrdinal = reader.GetOrdinal(CustomersSql.FirstNameColumn);
        int lastNameOrdinal = reader.GetOrdinal(CustomersSql.LastNameColumn);
        int emailOrdinal = reader.GetOrdinal(CustomersSql.EmailColumn);
        int revenueOrdinal = reader.GetOrdinal(CustomersSql.RevenueColumn);
        int ordersOrdinal = reader.GetOrdinal(CustomersSql.OrdersColumn);
        int quantityOrdinal = reader.GetOrdinal(CustomersSql.QuantityColumn);
        int previousRevenueOrdinal = reader.GetOrdinal(CustomersSql.PreviousRevenueColumn);
        int previousOrdersOrdinal = reader.GetOrdinal(CustomersSql.PreviousOrdersColumn);
        int previousQuantityOrdinal = reader.GetOrdinal(CustomersSql.PreviousQuantityColumn);
        int revenueRankOrdinal = reader.GetOrdinal(CustomersSql.RevenueRankColumn);
        int ordersRankOrdinal = reader.GetOrdinal(CustomersSql.OrdersRankColumn);
        int quantityRankOrdinal = reader.GetOrdinal(CustomersSql.QuantityRankColumn);

        string? GetString(int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

        var rows = new List<CustomersCustomerRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.GetInt32(idOrdinal),
                GetString(firstNameOrdinal),
                GetString(lastNameOrdinal),
                GetString(emailOrdinal),
                reader.GetDecimal(revenueOrdinal),
                reader.GetInt32(ordersOrdinal),
                reader.GetDecimal(quantityOrdinal),
                reader.GetDecimal(previousRevenueOrdinal),
                reader.GetInt32(previousOrdersOrdinal),
                reader.GetDecimal(previousQuantityOrdinal),
                (int)reader.GetInt64(revenueRankOrdinal),
                (int)reader.GetInt64(ordersRankOrdinal),
                (int)reader.GetInt64(quantityRankOrdinal)));
        }

        return rows;
    }
}
