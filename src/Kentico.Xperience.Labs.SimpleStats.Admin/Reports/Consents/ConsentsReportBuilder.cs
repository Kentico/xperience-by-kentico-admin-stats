using System.Globalization;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Consents;

/// <summary>
/// Turns aggregated consent data into the consents report.
/// </summary>
internal static class ConsentsReportBuilder
{
    /// <summary>
    /// Rows of the consents list.
    /// </summary>
    public const int ConsentLimit = 25;

    public static StatsSeriesDefinition AgreementsSeries { get; } = new("agreements", "Agreements");

    public static StatsSeriesDefinition RevocationsSeries { get; } = new("revocations", "Revocations");

    public static StatsSeriesDefinition AgreedContactsSeries { get; } = new("agreed", "Agreed contacts");

    /// <summary>
    /// Builds the report.
    /// </summary>
    /// <param name="query">Normalized filter. A consent ID that is not in <see cref="ConsentsReportData.Consents"/> means all consents.</param>
    /// <param name="data">Data from the start of the previous period (see <see cref="StatsComparison.GetPreviousRange"/>) to the end of the range.</param>
    /// <param name="getConsentPath">Returns the admin path of a consent's agreements, or <c>null</c>.</param>
    public static ConsentsResult Build(ConsentsQuery query, ConsentsReportData data, Func<int, string?>? getConsentPath = null)
    {
        var range = query.Range with { ChannelId = null };
        var (previousFrom, previousTo) = StatsComparison.GetPreviousRange(range);

        // Same rule as the SQL: an unknown consent means all consents.
        int? consentId = query.ConsentId is int id && data.Consents.Any(c => c.ConsentId == id) ? id : null;

        var agreementValues = data.Daily.Select(row => new StatsDailyValue(AgreementsSeries.Key, row.Date, row.Agreements)).ToList();
        var revocationValues = data.Daily.Select(row => new StatsDailyValue(RevocationsSeries.Key, row.Date, row.Revocations)).ToList();

        // Rows before the range are the previous period; the series use only the range.
        var agreements = StatsTimeSeriesBuilder.BuildValueSeries(range, agreementValues, AgreementsSeries, StatsValueKind.Count);
        var revocations = StatsTimeSeriesBuilder.BuildValueSeries(range, revocationValues, RevocationsSeries, StatsValueKind.Count);

        int agreedBefore = Math.Max(data.AgreedBeforeRange, 0);
        var agreedContacts = StatsTimeSeriesBuilder.BuildCumulativeSeries(
            range,
            data.Daily.Select(row => new StatsDailyValue(AgreedContactsSeries.Key, row.Date, row.AgreedChange)),
            AgreedContactsSeries,
            StatsValueKind.Count,
            agreedBefore);

        (int Agreements, int Revocations) Sum(DateOnly from, DateOnly to)
        {
            var rows = data.Daily.Where(row => row.Date >= from && row.Date <= to).ToList();
            return (rows.Sum(row => Math.Max(row.Agreements, 0)), rows.Sum(row => Math.Max(row.Revocations, 0)));
        }

        var (agreementCount, revocationCount) = Sum(range.From, range.To);
        var (previousAgreements, previousRevocations) = Sum(previousFrom, previousTo);

        var totals = new ConsentsTotals(
            StatsValueComparison.Create(range, StatsValueKind.Count, agreementCount, previousAgreements),
            StatsValueComparison.Create(range, StatsValueKind.Count, revocationCount, previousRevocations),
            StatsValueComparison.Create(
                range,
                StatsValueKind.Ratio,
                StatsValues.Divide(revocationCount, agreementCount),
                StatsValues.Divide(previousRevocations, previousAgreements)),
            // Point in time: on the range end vs on the previous period end (= the day before the range).
            StatsValueComparison.Create(range, StatsValueKind.Count, agreedContacts.Total, agreedBefore));

        return new(
            range.From,
            range.To,
            range.Grouping,
            consentId,
            [.. data.Consents.Select(row => new ConsentOption(row.ConsentId, GetConsentName(row)))],
            StatsPeriods.Build(range.From, range.To, range.Grouping),
            agreements,
            revocations,
            agreedContacts,
            totals,
            BuildByConsent(range, data, getConsentPath),
            BuildTextVersions(data, consentId),
            data.Available);
    }

    /// <summary>
    /// Display name of a consent, else "Consent #ID".
    /// </summary>
    public static string GetConsentName(ConsentsConsentRow row) =>
        string.IsNullOrWhiteSpace(row.DisplayName)
            ? string.Create(CultureInfo.InvariantCulture, $"Consent #{row.ConsentId}")
            : row.DisplayName.Trim();

    private static string GetKey(int consentId) => string.Create(CultureInfo.InvariantCulture, $"consent:{consentId}");

    /// <summary>
    /// All consents by agreed contacts on the range end. A contact can agree to several consents, so shares are of all agreed contacts.
    /// Consents without agreed contacts are kept (value 0) so every consent can be compared.
    /// </summary>
    private static StatsRankedResult BuildByConsent(StatsQuery range, ConsentsReportData data, Func<int, string?>? getConsentPath)
    {
        var entries = data.Consents.Select(row => new StatsRankedEntry(
            Key: GetKey(row.ConsentId),
            Label: GetConsentName(row),
            SecondaryLabel: null,
            Value: Math.Max(row.AgreedContacts, 0),
            SecondaryValue: Math.Max(row.Agreements, 0),
            Url: null)
        {
            AdminPath = getConsentPath?.Invoke(row.ConsentId),
            PreviousValue = Math.Max(row.PreviousAgreedContacts, 0),
            TertiaryValue = Math.Max(row.Revocations, 0),
        });

        var ranked = StatsRankedBuilder.BuildSnapshot(
            channelId: null,
            entries,
            Math.Max(data.AllAgreedContacts, 0),
            data.Consents.Count,
            ConsentLimit,
            includeZero: true,
            itemsOverlap: true);

        // Point-in-time values on the range end, but the list belongs to the range (previous values are of the previous period end).
        return ranked with { From = range.From, To = range.To };
    }

    /// <summary>
    /// Agreed contacts on the current text (covered) of all agreed contacts (total), per consent with agreed contacts (consent filter applied).
    /// </summary>
    private static IReadOnlyList<StatsCoverageItem> BuildTextVersions(ConsentsReportData data, int? consentId) =>
        [.. data.Consents
            .Where(row => consentId is null || row.ConsentId == consentId)
            .Where(row => row.AgreedContacts > 0)
            .OrderByDescending(row => row.AgreedContacts)
            .ThenBy(row => row.ConsentId)
            .Select(row => new StatsCoverageItem(
                GetKey(row.ConsentId),
                GetConsentName(row),
                null,
                Math.Max(row.AgreedContacts - Math.Clamp(row.OlderText, 0, row.AgreedContacts), 0),
                row.AgreedContacts))];
}
