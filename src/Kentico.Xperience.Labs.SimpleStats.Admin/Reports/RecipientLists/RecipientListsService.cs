using System.Globalization;

using CMS.EmailMarketing;
using CMS.Helpers;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.DigitalMarketing.UIPages;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

using Microsoft.Extensions.Options;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.RecipientLists;

/// <summary>
/// Builds the recipient lists report.
/// </summary>
public interface IRecipientListsService
{
    /// <summary>
    /// Returns the report for the query.
    /// </summary>
    /// <param name="query">Normalized filter (see <see cref="RecipientListsFilter.Normalize"/>). A list ID that does not exist means all lists.</param>
    /// <param name="refresh">When <c>true</c>, cached data is dropped and read again from the database (then cached again).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<RecipientListsResult> GetReport(RecipientListsQuery query, bool refresh, CancellationToken cancellationToken);
}

internal sealed class RecipientListsService(
    IRecipientListsRepository repository,
    IProgressiveCache cache,
    IStatsCacheInvalidator cacheInvalidator,
    IStatsAdminLinks adminLinks,
    IOptionsMonitor<BouncedEmailsGlobalOptions> bounceOptions,
    TimeProvider clock) : IRecipientListsService
{
    private readonly IRecipientListsRepository repository = repository;
    private readonly IProgressiveCache cache = cache;
    private readonly IStatsCacheInvalidator cacheInvalidator = cacheInvalidator;
    private readonly IStatsAdminLinks adminLinks = adminLinks;
    private readonly IOptionsMonitor<BouncedEmailsGlobalOptions> bounceOptions = bounceOptions;
    private readonly TimeProvider clock = clock;

    public async Task<RecipientListsResult> GetReport(RecipientListsQuery query, bool refresh, CancellationToken cancellationToken)
    {
        // Recipient lists have no channel, so the channel is dropped and does not split the cache key.
        var normalized = new RecipientListsQuery(query.Range with { ChannelId = null }, query.RecipientListId is > 0 ? query.RecipientListId : null);
        var range = normalized.Range;
        var (previousFrom, _) = StatsComparison.GetPreviousRange(range);

        // Same limit as the native recipient list overview (app options, default 5).
        int softBounceLimit = bounceOptions.CurrentValue.SoftBounceLimit;

        // Cache the daily aggregate (not the bucketed result) so switching grouping does not hit the database.
        var settings = StatsCache.CreateSettings(
            "recipient-lists",
            range.From.DayNumber,
            range.To.DayNumber,
            normalized.RecipientListId?.ToString(CultureInfo.InvariantCulture) ?? "all",
            softBounceLimit);

        var snapshot = await cache.LoadAsync(
            cacheInvalidator,
            settings,
            refresh,
            async token => new RecipientListsSnapshot(
                await repository.GetData(previousFrom, range.From, range.To, normalized.RecipientListId, softBounceLimit, token),
                clock.GetUtcNow()),
            cancellationToken);

        var result = RecipientListsReportBuilder.Build(normalized, snapshot.Data, GetListPath);

        return result with
        {
            RecipientListsAppPath = adminLinks.GetPath<RecipientListList>(),
            UpdatedAt = snapshot.ReadAt,
        };
    }

    /// <summary>
    /// Path of the list in the native Recipient lists application (opens its overview).
    /// </summary>
    private string? GetListPath(int listId) =>
        adminLinks.GetPath<RecipientListEditSection>(new PageParameterValues
        {
            { typeof(RecipientListEditSection), listId },
        });
}
