using System.Globalization;

using CMS.Helpers;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.DigitalMarketing.UIPages;
using Kentico.Xperience.AdminStats.Shared;

namespace Kentico.Xperience.AdminStats.Reports.Consents;

/// <summary>
/// Builds the consents report.
/// </summary>
public interface IConsentsService
{
    /// <summary>
    /// Returns the report for the query.
    /// </summary>
    /// <param name="query">Normalized filter (see <see cref="ConsentsFilter.Normalize"/>). A consent ID that does not exist means all consents.</param>
    /// <param name="refresh">When <c>true</c>, cached data is dropped and read again from the database (then cached again).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<ConsentsResult> GetReport(ConsentsQuery query, bool refresh, CancellationToken cancellationToken);
}

internal sealed class ConsentsService(
    IConsentsRepository repository,
    IProgressiveCache cache,
    IStatsCacheInvalidator cacheInvalidator,
    IStatsAdminLinks adminLinks,
    TimeProvider clock) : IConsentsService
{
    private readonly IConsentsRepository repository = repository;
    private readonly IProgressiveCache cache = cache;
    private readonly IStatsCacheInvalidator cacheInvalidator = cacheInvalidator;
    private readonly IStatsAdminLinks adminLinks = adminLinks;
    private readonly TimeProvider clock = clock;

    public async Task<ConsentsResult> GetReport(ConsentsQuery query, bool refresh, CancellationToken cancellationToken)
    {
        // Consents have no channel, so the channel is dropped and does not split the cache key.
        var normalized = new ConsentsQuery(query.Range with { ChannelId = null }, query.ConsentId is > 0 ? query.ConsentId : null);
        var range = normalized.Range;

        // One batch reads the previous period and the range; the builder splits the rows by date.
        var (previousFrom, _) = StatsComparison.GetPreviousRange(range);

        // Cache the daily aggregate (not the bucketed result) so switching grouping does not hit the database.
        var settings = StatsCache.CreateSettings(
            "consents",
            range.From.DayNumber,
            range.To.DayNumber,
            normalized.ConsentId?.ToString(CultureInfo.InvariantCulture) ?? "all");

        var snapshot = await cache.LoadAsync(
            cacheInvalidator,
            settings,
            refresh,
            async token => new ConsentsSnapshot(
                await repository.GetData(previousFrom, range.From, range.To, normalized.ConsentId, token),
                clock.GetUtcNow()),
            cancellationToken);

        // The Data protection application's pages are per content language (consent texts are), agreements are not: use the default language.
        string? languageName = snapshot.Data.DefaultLanguageName;

        var result = ConsentsReportBuilder.Build(normalized, snapshot.Data, consentId => GetAgreementsPath(languageName, consentId));

        return result with
        {
            ByConsent = result.ByConsent with { UpdatedAt = snapshot.ReadAt },
            DataProtectionPath = languageName is null
                ? null
                : adminLinks.GetPath<ConsentList>(new PageParameterValues
                {
                    { typeof(DataProtectionContentLanguage), languageName },
                }),
            UpdatedAt = snapshot.ReadAt,
        };
    }

    /// <summary>
    /// Path of the consent's "Consent agreements" tab (contacts who agree now) in the native Data protection application.
    /// </summary>
    private string? GetAgreementsPath(string? languageName, int consentId) =>
        languageName is null
            ? null
            : adminLinks.GetPath<ConsentAgreementList>(new PageParameterValues
            {
                { typeof(DataProtectionContentLanguage), languageName },
                { typeof(ConsentEditSection), consentId },
            });
}
