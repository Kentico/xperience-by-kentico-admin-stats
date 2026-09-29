using CMS.Helpers;

using Kentico.Xperience.AdminStats.Shared;

namespace Kentico.Xperience.AdminStats.Reports.ActivityCounts;

/// <summary>
/// Builds the activity counts report.
/// </summary>
public interface IActivityCountsService
{
    /// <summary>
    /// Returns the report for the query.
    /// </summary>
    /// <param name="query">Normalized filter.</param>
    /// <param name="refresh">When <c>true</c>, cached data is dropped and read again from the database (then cached again).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<ActivityCountsResult> GetReport(StatsQuery query, bool refresh, CancellationToken cancellationToken);
}

internal sealed class ActivityCountsService(
    IActivityCountsRepository repository,
    IProgressiveCache cache,
    IStatsCacheInvalidator cacheInvalidator,
    TimeProvider clock) : IActivityCountsService
{
    /// <summary>
    /// Cache expiry. Only this expiry or an explicit refresh drops cached data; data changes do not.
    /// </summary>
    internal const double CacheMinutes = 5;

    private readonly IActivityCountsRepository repository = repository;
    private readonly IProgressiveCache cache = cache;
    private readonly IStatsCacheInvalidator cacheInvalidator = cacheInvalidator;
    private readonly TimeProvider clock = clock;

    public async Task<ActivityCountsResult> GetReport(StatsQuery query, bool refresh, CancellationToken cancellationToken)
    {
        // Cache the daily aggregate (not the bucketed result) so switching grouping does not hit the database.
        var dailyCountsSettings = new CacheSettings(
            CacheMinutes,
            StatsCache.ItemNamePrefix,
            "activity-counts",
            query.From.DayNumber,
            query.To.DayNumber,
            query.ChannelId ?? 0);

        var displayNamesSettings = new CacheSettings(CacheMinutes, StatsCache.ItemNamePrefix, "activity-type-names");

        if (refresh)
        {
            cacheInvalidator.Remove(dailyCountsSettings);
            cacheInvalidator.Remove(displayNamesSettings);
        }

        var dailyCounts = await cache.LoadAsync(
            async (_, token) => new ActivityDailyCountsSnapshot(
                await repository.GetDailyCounts(query.From, query.To, query.ChannelId, token),
                clock.GetUtcNow()),
            dailyCountsSettings,
            cancellationToken);

        var displayNames = await cache.LoadAsync(
            (_, token) => repository.GetActivityTypeDisplayNames(token),
            displayNamesSettings,
            cancellationToken);

        return ActivityCountsReportBuilder.Build(query, dailyCounts.Rows, displayNames) with { UpdatedAt = dailyCounts.ReadAt };
    }
}
