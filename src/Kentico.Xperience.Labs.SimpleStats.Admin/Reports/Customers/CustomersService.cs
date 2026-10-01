using System.Globalization;

using CMS.Helpers;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.DigitalCommerce.UIPages;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Commerce;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Customers;

/// <summary>
/// Builds the customers report.
/// </summary>
public interface ICustomersService
{
    /// <summary>
    /// Returns the report for the query.
    /// </summary>
    /// <param name="query">Normalized filter (see <see cref="CustomersFilter.Normalize"/>). A status ID that does not exist means all statuses.</param>
    /// <param name="refresh">When <c>true</c>, cached data is dropped and read again from the database (then cached again).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<CustomersResult> GetReport(CustomersQuery query, bool refresh, CancellationToken cancellationToken);
}

internal sealed class CustomersService(
    ICustomersRepository repository,
    IProgressiveCache cache,
    IStatsCacheInvalidator cacheInvalidator,
    IStatsAdminLinks adminLinks,
    IStatsAmountFormatter amountFormatter,
    TimeProvider clock) : ICustomersService
{
    private readonly ICustomersRepository repository = repository;
    private readonly IProgressiveCache cache = cache;
    private readonly IStatsCacheInvalidator cacheInvalidator = cacheInvalidator;
    private readonly IStatsAdminLinks adminLinks = adminLinks;
    private readonly IStatsAmountFormatter amountFormatter = amountFormatter;
    private readonly TimeProvider clock = clock;

    public async Task<CustomersResult> GetReport(CustomersQuery query, bool refresh, CancellationToken cancellationToken)
    {
        // Statuses are editable per project, so the filter is checked against the current statuses (cached like the data, shared with orders and revenue).
        var statuses = await cache.LoadAsync(
            cacheInvalidator,
            StatsCache.CreateSettings(CommerceOrderStatuses.CacheName),
            refresh,
            repository.GetStatuses,
            cancellationToken);

        int? statusId = CommerceOrderStatuses.Resolve(statuses, query.OrderStatusId);

        // Customers and orders have no channel, so the channel is dropped and does not split the cache key.
        var normalized = new CustomersQuery(
            query.Range with { ChannelId = null },
            statusId,
            query.AddressType,
            CustomersActivityWindow.Normalize(query.ActivityWindowDays));
        var range = normalized.Range;

        // One batch reads the previous period and the range; the builder splits the rows by date.
        var (previousFrom, _) = StatsComparison.GetPreviousRange(range);

        // Cache the daily aggregate (not the bucketed result) so switching grouping does not hit the database.
        var settings = StatsCache.CreateSettings(
            "customers",
            range.From.DayNumber,
            range.To.DayNumber,
            statusId?.ToString(CultureInfo.InvariantCulture) ?? "all",
            normalized.AddressType.ToString().ToLowerInvariant(),
            normalized.ActivityWindowDays);

        var snapshot = await cache.LoadAsync(
            cacheInvalidator,
            settings,
            refresh,
            async token => new CustomersSnapshot(
                await repository.GetData(
                    previousFrom,
                    range.From,
                    range.To,
                    statusId,
                    normalized.AddressType,
                    CustomersReportBuilder.TopLimit,
                    CustomersReportBuilder.CountryLimit,
                    normalized.ActivityWindowDays,
                    token),
                clock.GetUtcNow()),
            cancellationToken);

        var result = CustomersReportBuilder.Build(normalized, snapshot.Data, statuses, GetCustomerPath);
        var top = result.TopCustomers;

        // Amounts get texts from the project's price formatter (currency), applied after the cache so formatter changes show at once.
        return result with
        {
            Totals = result.Totals with { RevenuePerCustomer = result.Totals.RevenuePerCustomer.WithTexts(amountFormatter) },
            ByCountry = result.ByCountry with { UpdatedAt = snapshot.ReadAt },
            TopStates = result.TopStates with { UpdatedAt = snapshot.ReadAt },
            TopCustomers = new(
                top.ByRevenue.WithTexts(amountFormatter) with { UpdatedAt = snapshot.ReadAt },
                top.ByOrders.WithTexts(amountFormatter) with { UpdatedAt = snapshot.ReadAt },
                top.ByQuantity.WithTexts(amountFormatter) with { UpdatedAt = snapshot.ReadAt }),
            CustomersPath = adminLinks.GetPath<CustomersList>(),
            UpdatedAt = snapshot.ReadAt,
        };
    }

    /// <summary>
    /// Path of the customer's overview in the native Customers application.
    /// </summary>
    private string? GetCustomerPath(int customerId) =>
        adminLinks.GetPath<CustomerOverview>(new PageParameterValues
        {
            { typeof(CustomerEditSection), customerId },
        });
}
