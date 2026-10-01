using CMS.Helpers;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TopPages;

/// <summary>
/// Builds the top pages report.
/// </summary>
public interface ITopPagesService
{
    /// <summary>
    /// Returns the most visited pages for the query. Grouping is ignored.
    /// </summary>
    /// <param name="query">Normalized filter.</param>
    /// <param name="refresh">When <c>true</c>, cached data is dropped and read again from the database (then cached again).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<StatsRankedResult> GetReport(StatsQuery query, bool refresh, CancellationToken cancellationToken);
}

internal sealed class TopPagesService(
    ITopPagesRepository repository,
    IProgressiveCache cache,
    IStatsCacheInvalidator cacheInvalidator,
    TimeProvider clock) : ITopPagesService
{
    /// <summary>
    /// Number of pages in the report. Fixed for now, not user input.
    /// </summary>
    internal const int Limit = 25;

    private readonly ITopPagesRepository repository = repository;
    private readonly IProgressiveCache cache = cache;
    private readonly IStatsCacheInvalidator cacheInvalidator = cacheInvalidator;
    private readonly TimeProvider clock = clock;

    public async Task<StatsRankedResult> GetReport(StatsQuery query, bool refresh, CancellationToken cancellationToken)
    {
        // Grouping is not part of the key: this report uses the range only.
        var settings = StatsCache.CreateSettings(
            "top-pages",
            query.From.DayNumber,
            query.To.DayNumber,
            query.ChannelId ?? 0,
            Limit);

        var snapshot = await cache.LoadAsync(
            cacheInvalidator,
            settings,
            refresh,
            async token => new TopPagesSnapshot(
                await repository.GetTopPages(query.From, query.To, query.ChannelId, Limit, token),
                clock.GetUtcNow()),
            cancellationToken);

        return TopPagesReportBuilder.Build(query, snapshot.Data, Limit) with { UpdatedAt = snapshot.ReadAt };
    }
}
