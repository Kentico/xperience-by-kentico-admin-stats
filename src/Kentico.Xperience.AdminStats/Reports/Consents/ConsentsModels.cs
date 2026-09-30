using Kentico.Xperience.AdminStats.Shared;

namespace Kentico.Xperience.AdminStats.Reports.Consents;

/// <summary>
/// Filter of the consents report, sent by the admin client: the shared range and grouping (<see cref="StatsFilter"/>)
/// and an optional consent. The shared filter is wrapped, not changed (same approach as the orders and revenue report),
/// so other reports keep their filter and cache keys.
/// </summary>
public sealed record ConsentsFilter
{
    /// <summary>
    /// Range and grouping. The channel is ignored (consents have no channel). <c>null</c> means defaults.
    /// </summary>
    public StatsFilter? Range { get; init; }

    /// <summary>
    /// Optional consent ID (<c>CMS_Consent.ConsentID</c>). <c>null</c>, a value &lt;= 0,
    /// or an ID that does not exist (checked by the query) means all consents.
    /// </summary>
    public int? ConsentId { get; init; }

    /// <summary>
    /// Applies defaults and limits and returns a query that is safe to run.
    /// </summary>
    /// <param name="today">Current date used for the default range.</param>
    public ConsentsQuery Normalize(DateOnly today) =>
        new((Range ?? new StatsFilter()).Normalize(today) with { ChannelId = null }, ConsentId is > 0 ? ConsentId : null);
}

/// <summary>
/// Normalized consents filter.
/// </summary>
/// <param name="Range">Range and grouping. <see cref="StatsQuery.ChannelId"/> is always <c>null</c>.</param>
/// <param name="ConsentId">Consent ID, or <c>null</c> for all consents. Unknown IDs are treated as all consents.</param>
public sealed record ConsentsQuery(StatsQuery Range, int? ConsentId);

/// <summary>
/// Input of the consents <c>LOAD</c> page command.
/// </summary>
public sealed record ConsentsLoadRequest
{
    /// <summary>
    /// Report filter. <c>null</c> means defaults.
    /// </summary>
    public ConsentsFilter? Filter { get; init; }

    /// <summary>
    /// When <c>true</c>, cached data for the filter is dropped and read again from the database.
    /// </summary>
    public bool Refresh { get; init; }
}

/// <summary>
/// Consent shown in the consent filter, by display name.
/// </summary>
public sealed record ConsentOption(int Id, string DisplayName);

/// <summary>
/// KPIs of the range compared with the previous period (consent filter applied).
/// </summary>
/// <param name="Agreements">Agree events in the period. Agreeing again (for example to a new text) counts again.</param>
/// <param name="Revocations">Revoke events in the period.</param>
/// <param name="RevocationRate">
/// Revocations / agreements in the period (ratio). <c>null</c> for a period without agreements. The change is in percentage points.
/// </param>
/// <param name="AgreedContacts">
/// Agreed contacts (see <see cref="ConsentsResult.AgreedContacts"/>) on the last day of the range vs the last day of the previous period.
/// A point-in-time count, not a sum over the period.
/// </param>
public sealed record ConsentsTotals(
    StatsValueComparison Agreements,
    StatsValueComparison Revocations,
    StatsValueComparison RevocationRate,
    StatsValueComparison AgreedContacts);

/// <summary>
/// Consents report.
/// </summary>
/// <param name="From">Applied range start (inclusive).</param>
/// <param name="To">Applied range end (inclusive).</param>
/// <param name="Grouping">Applied grouping.</param>
/// <param name="ConsentId">Applied consent filter, <c>null</c> for all consents.</param>
/// <param name="Consents">Consents for the consent filter, by display name.</param>
/// <param name="Periods">Period axis of the series.</param>
/// <param name="Agreements">Agree events per period.</param>
/// <param name="Revocations">Revoke events per period.</param>
/// <param name="AgreedContacts">
/// Agreed contacts at the end of each period (the range end for the last, partial period): contacts whose latest agreement row
/// of the consent on or before that day is not revoked. With all consents, distinct contacts who agree to at least one consent.
/// A point-in-time count; <see cref="StatsValueSeries.Total"/> is the value at the range end, not a sum.
/// </param>
/// <param name="Totals">KPIs vs the previous period.</param>
/// <param name="ByConsent">
/// All consents (the consent filter does not apply, so consents can be compared). Value = agreed contacts on the range end, with the
/// value on the previous period end and change; secondary value = agreements, tertiary value = revocations in the range.
/// A contact can agree to several consents; shares are of all agreed contacts.
/// </param>
/// <param name="TextVersions">
/// Per consent (consent filter applied): agreed contacts on the range end (total) and of them contacts whose latest agreement
/// was given to the current consent text (covered). The others agreed to an older text.
/// </param>
/// <param name="Available"><c>false</c> when the consent tables do not exist; the report is then empty.</param>
public sealed record ConsentsResult(
    DateOnly From,
    DateOnly To,
    StatsGrouping Grouping,
    int? ConsentId,
    IReadOnlyList<ConsentOption> Consents,
    IReadOnlyList<StatsPeriod> Periods,
    StatsValueSeries Agreements,
    StatsValueSeries Revocations,
    StatsValueSeries AgreedContacts,
    ConsentsTotals Totals,
    StatsRankedResult ByConsent,
    IReadOnlyList<StatsCoverageItem> TextVersions,
    bool Available)
{
    /// <summary>
    /// Path of the native Data protection application's consent listing, relative to the admin root. <c>null</c> when it is not available.
    /// </summary>
    public string? DataProtectionPath { get; init; }

    /// <summary>
    /// When the data was read from the database. Can be older than the request when served from cache.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Consent events on one day (consent filter applied), as returned by the SQL aggregate.
/// </summary>
/// <param name="Date">Day of the events.</param>
/// <param name="Agreements">Agree events.</param>
/// <param name="Revocations">Revoke events.</param>
/// <param name="AgreedChange">
/// Change of the agreed contacts count on that day (contacts who became agreed minus contacts who stopped being agreed).
/// Summing the changes up to a day gives the agreed contacts on that day.
/// </param>
public sealed record ConsentsDailyRow(DateOnly Date, int Agreements, int Revocations, int AgreedChange);

/// <summary>
/// One consent with its agreement numbers (consent filter does not apply).
/// </summary>
/// <param name="ConsentId">Consent ID.</param>
/// <param name="DisplayName">Consent display name.</param>
/// <param name="Agreements">Agree events in the range.</param>
/// <param name="PreviousAgreements">Agree events in the previous period.</param>
/// <param name="Revocations">Revoke events in the range.</param>
/// <param name="AgreedContacts">Agreed contacts on the range end.</param>
/// <param name="PreviousAgreedContacts">Agreed contacts on the previous period end.</param>
/// <param name="OlderText">Of <paramref name="AgreedContacts"/>, contacts whose latest agreement hash is not the consent's current hash.</param>
public sealed record ConsentsConsentRow(
    int ConsentId,
    string? DisplayName,
    int Agreements,
    int PreviousAgreements,
    int Revocations,
    int AgreedContacts,
    int PreviousAgreedContacts,
    int OlderText);

/// <summary>
/// Aggregated consent data. <see cref="Daily"/> starts at the previous period.
/// </summary>
/// <param name="Available"><c>false</c> when the consent tables do not exist.</param>
/// <param name="Consents">All consents, by display name.</param>
/// <param name="Daily">Events per day, previous period + range (consent filter applied).</param>
/// <param name="AgreedBeforeRange">Agreed contacts on the last day before the range (consent filter applied).</param>
/// <param name="AllAgreedContacts">Distinct agreed contacts (any consent) on the range end, the total of the per consent list.</param>
public sealed record ConsentsReportData(
    bool Available,
    IReadOnlyList<ConsentsConsentRow> Consents,
    IReadOnlyList<ConsentsDailyRow> Daily,
    int AgreedBeforeRange,
    int AllAgreedContacts)
{
    /// <summary>
    /// Code name of the default content language, used for links to the Data protection application (its pages are per language).
    /// <c>null</c> when there is none.
    /// </summary>
    public string? DefaultLanguageName { get; init; }

    public static ConsentsReportData Empty { get; } = new(true, [], [], 0, 0);

    public static ConsentsReportData Unavailable { get; } = Empty with { Available = false };
}

/// <summary>
/// Consent data with the time it was read. This is the cached value.
/// </summary>
internal sealed record ConsentsSnapshot(ConsentsReportData Data, DateTimeOffset ReadAt);
