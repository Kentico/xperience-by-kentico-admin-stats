using CMS.Helpers;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.UIPages;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Members;

/// <summary>
/// Builds the member registrations report.
/// </summary>
public interface IMembersService
{
    /// <summary>
    /// Returns the report for the query. The channel is ignored: members are global.
    /// </summary>
    /// <param name="query">Normalized filter.</param>
    /// <param name="refresh">When <c>true</c>, cached data is dropped and read again from the database (then cached again).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<MembersResult> GetReport(StatsQuery query, bool refresh, CancellationToken cancellationToken);
}

internal sealed class MembersService(
    IMembersRepository repository,
    IProgressiveCache cache,
    IStatsCacheInvalidator cacheInvalidator,
    IStatsAdminLinks adminLinks,
    TimeProvider clock) : IMembersService
{
    private readonly IMembersRepository repository = repository;
    private readonly IProgressiveCache cache = cache;
    private readonly IStatsCacheInvalidator cacheInvalidator = cacheInvalidator;
    private readonly IStatsAdminLinks adminLinks = adminLinks;
    private readonly TimeProvider clock = clock;

    public async Task<MembersResult> GetReport(StatsQuery query, bool refresh, CancellationToken cancellationToken)
    {
        // Members have no channel, so the channel is dropped and does not split the cache key.
        var range = query with { ChannelId = null };

        // One batch reads the previous period and the range; the builder splits the rows by date.
        var (previousFrom, _) = StatsComparison.GetPreviousRange(range);

        // Cache the daily aggregate (not the bucketed result) so switching grouping does not hit the database.
        var settings = StatsCache.CreateSettings("members", range.From.DayNumber, range.To.DayNumber);

        var snapshot = await cache.LoadAsync(
            cacheInvalidator,
            settings,
            refresh,
            async token => new MembersSnapshot(
                await repository.GetData(previousFrom, range.From, range.To, MembersReportBuilder.RoleLimit, token),
                clock.GetUtcNow()),
            cancellationToken);

        var result = MembersReportBuilder.Build(range, snapshot.Data, GetRolePath);

        return result with
        {
            ByRole = result.ByRole with { UpdatedAt = snapshot.ReadAt },
            MembersPath = adminLinks.GetPath<MemberList>(),
            UpdatedAt = snapshot.ReadAt,
        };
    }

    /// <summary>
    /// Path of the member role's edit page in the native Members application.
    /// </summary>
    private string? GetRolePath(int roleId) =>
        adminLinks.GetPath<MemberRoleEdit>(new PageParameterValues
        {
            { typeof(MemberRoleEditSection), roleId },
        });
}
