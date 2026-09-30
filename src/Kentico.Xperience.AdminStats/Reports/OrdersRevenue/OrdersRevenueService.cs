using CMS.Helpers;

using Kentico.Xperience.Admin.DigitalCommerce.UIPages;
using Kentico.Xperience.AdminStats.Reports.Commerce;
using Kentico.Xperience.AdminStats.Shared;

namespace Kentico.Xperience.AdminStats.Reports.OrdersRevenue;

/// <summary>
/// Builds the orders and revenue report.
/// </summary>
public interface IOrdersRevenueService
{
    /// <summary>
    /// Returns the report for the query.
    /// </summary>
    /// <param name="query">Normalized filter (see <see cref="OrdersRevenueFilter.Normalize"/>). A status ID that does not exist means all statuses.</param>
    /// <param name="refresh">When <c>true</c>, cached data is dropped and read again from the database (then cached again).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<OrdersRevenueResult> GetReport(OrdersRevenueQuery query, bool refresh, CancellationToken cancellationToken);
}

internal sealed class OrdersRevenueService(
    IOrdersRevenueRepository repository,
    IProgressiveCache cache,
    IStatsCacheInvalidator cacheInvalidator,
    IStatsAdminLinks adminLinks,
    IStatsAmountFormatter amountFormatter,
    TimeProvider clock) : IOrdersRevenueService
{
    private readonly IOrdersRevenueRepository repository = repository;
    private readonly IProgressiveCache cache = cache;
    private readonly IStatsCacheInvalidator cacheInvalidator = cacheInvalidator;
    private readonly IStatsAdminLinks adminLinks = adminLinks;
    private readonly IStatsAmountFormatter amountFormatter = amountFormatter;
    private readonly TimeProvider clock = clock;

    public async Task<OrdersRevenueResult> GetReport(OrdersRevenueQuery query, bool refresh, CancellationToken cancellationToken)
    {
        // Statuses are editable per project, so the filter is checked against the current statuses (cached like the data).
        var statuses = await cache.LoadAsync(
            cacheInvalidator,
            StatsCache.CreateSettings(CommerceOrderStatuses.CacheName),
            refresh,
            repository.GetStatuses,
            cancellationToken);

        int? statusId = CommerceOrderStatuses.Resolve(statuses, query.OrderStatusId);

        // Orders have no channel, so the channel is dropped and does not split the cache key.
        var normalized = new OrdersRevenueQuery(query.Range with { ChannelId = null }, statusId);
        var range = normalized.Range;

        // One batch reads the previous period and the range; the builder splits the rows by date.
        var (previousFrom, _) = StatsComparison.GetPreviousRange(range);

        // Cache the daily aggregate (not the bucketed result) so switching grouping does not hit the database.
        var settings = StatsCache.CreateSettings(
            "orders-revenue",
            range.From.DayNumber,
            range.To.DayNumber,
            statusId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "all");

        var snapshot = await cache.LoadAsync(
            cacheInvalidator,
            settings,
            refresh,
            async token => new OrdersRevenueSnapshot(
                await repository.GetData(previousFrom, range.From, range.To, statusId, OrdersRevenueReportBuilder.TopLimit, token),
                clock.GetUtcNow()),
            cancellationToken);

        var result = OrdersRevenueReportBuilder.Build(normalized, snapshot.Data, statuses);
        var totals = result.Totals;

        // Amounts get texts from the project's price formatter (currency), applied after the cache so formatter changes show at once.
        return result with
        {
            Revenue = result.Revenue.WithTexts(amountFormatter),
            Totals = totals with
            {
                Revenue = totals.Revenue.WithTexts(amountFormatter),
                AverageOrderValue = totals.AverageOrderValue.WithTexts(amountFormatter),
            },
            ByStatus = result.ByStatus.WithTexts(amountFormatter) with { UpdatedAt = snapshot.ReadAt },
            TopProducts = result.TopProducts.WithTexts(amountFormatter) with { UpdatedAt = snapshot.ReadAt },
            OrdersPath = adminLinks.GetPath<OrdersList>(),
            UpdatedAt = snapshot.ReadAt,
        };
    }
}
