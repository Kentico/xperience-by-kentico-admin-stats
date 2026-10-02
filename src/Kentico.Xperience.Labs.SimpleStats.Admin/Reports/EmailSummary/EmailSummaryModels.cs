using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EmailSummary;

/// <summary>
/// Lifetime numbers of one email from the statistics table (<c>CMS.EmailLibrary.EmailStatisticsInfo</c>), the same numbers as the
/// native Statistics tab of the email. The table has no dates.
/// </summary>
/// <param name="Sent">Sent emails (<c>EmailStatisticsTotalSent</c>).</param>
/// <param name="Delivered">Delivered emails (sent minus bounces, or set by the delivery provider).</param>
/// <param name="UniqueOpens">Recipients who opened the email (a click counts as an open).</param>
/// <param name="UniqueClicks">Recipients who clicked a link.</param>
/// <param name="SoftBounces">Soft bounces. <c>null</c> when the delivery provider does not track them.</param>
/// <param name="HardBounces">Hard bounces. <c>null</c> when the delivery provider does not track them.</param>
/// <param name="Unsubscribes">Recipients who unsubscribed through the email.</param>
/// <param name="SpamReports">Spam reports. <c>null</c> when the delivery provider does not track them.</param>
public sealed record EmailStatisticsValues(
    int Sent,
    int Delivered,
    int UniqueOpens,
    int UniqueClicks,
    int? SoftBounces,
    int? HardBounces,
    int Unsubscribes,
    int? SpamReports)
{
    public static EmailStatisticsValues Zero { get; } = new(0, 0, 0, 0, null, null, 0, null);
}

/// <summary>
/// Rates of one email or a sum of emails, as the native Statistics tab computes them. <c>null</c> when the denominator is 0.
/// Rates can be over 1 (100%) when the data has opens or clicks of recipients without a sent record.
/// </summary>
/// <param name="DeliveryRate">Delivered / sent.</param>
/// <param name="OpenRate">Unique opens / delivered.</param>
/// <param name="ClickRate">Unique clicks / delivered.</param>
/// <param name="UnsubscribeRate">Unsubscribes / sent.</param>
public sealed record EmailRates(decimal? DeliveryRate, decimal? OpenRate, decimal? ClickRate, decimal? UnsubscribeRate)
{
    public static EmailRates From(EmailStatisticsValues values) =>
        new(
            Round(StatsValues.Divide(values.Delivered, values.Sent)),
            Round(StatsValues.Divide(values.UniqueOpens, values.Delivered)),
            Round(StatsValues.Divide(values.UniqueClicks, values.Delivered)),
            Round(StatsValues.Divide(values.Unsubscribes, values.Sent)));

    private static decimal? Round(decimal? value) => StatsValues.Round(value, StatsValueKind.Ratio);
}

/// <summary>
/// One regular email sent in the range (send date in the range), with its lifetime statistics.
/// </summary>
/// <param name="Id">Email configuration ID.</param>
/// <param name="Name">Display name in the channel's primary language (else another language, else the code name).</param>
/// <param name="SendTime">Send date (<c>SendConfigurationScheduledTime</c>), server local time.</param>
/// <param name="RecipientList">Display name of the recipient list. <c>null</c> when unknown.</param>
/// <param name="Statistics">Lifetime numbers. Zero when the email has no statistics yet.</param>
/// <param name="HasStatistics"><c>false</c> when the email has no statistics row (not calculated yet).</param>
/// <param name="Rates">Rates of <paramref name="Statistics"/>.</param>
public sealed record EmailSummaryEmail(
    int Id,
    string Name,
    DateTime SendTime,
    string? RecipientList,
    EmailStatisticsValues Statistics,
    bool HasStatistics,
    EmailRates Rates)
{
    /// <summary>
    /// Path of the email's Statistics tab, relative to the admin root. <c>null</c> when it is not available.
    /// </summary>
    public string? StatisticsPath { get; init; }
}

/// <summary>
/// One automated email (any purpose other than regular) with statistics. Lifetime numbers, not limited to the range.
/// </summary>
/// <param name="Id">Email configuration ID.</param>
/// <param name="Name">Display name (see <see cref="EmailSummaryEmail.Name"/>).</param>
/// <param name="Purpose">Email purpose (<c>CMS.EmailLibrary.EmailPurpose</c>), for example <c>Automation</c>.</param>
/// <param name="SentInRange">Sent records (hits) in the range.</param>
/// <param name="Statistics">Lifetime numbers.</param>
/// <param name="Rates">Rates of <paramref name="Statistics"/>.</param>
public sealed record EmailSummaryAutomatedEmail(
    int Id,
    string Name,
    string Purpose,
    int SentInRange,
    EmailStatisticsValues Statistics,
    EmailRates Rates)
{
    /// <inheritdoc cref="EmailSummaryEmail.StatisticsPath"/>
    public string? StatisticsPath { get; init; }
}

/// <summary>
/// KPIs of the regular emails sent in the range vs those sent in the previous period. Sums of the lifetime numbers of these emails;
/// rates are sums divided by sums (not averages of the per-email rates). Rate changes are in percentage points.
/// Bounces and spam reports are <c>null</c> when no email of the period has them (not tracked by the delivery provider).
/// </summary>
public sealed record EmailSummaryTotals(
    StatsValueComparison Emails,
    StatsValueComparison Sent,
    StatsValueComparison Delivered,
    StatsValueComparison DeliveryRate,
    StatsValueComparison OpenRate,
    StatsValueComparison ClickRate,
    StatsValueComparison HardBounces,
    StatsValueComparison SoftBounces,
    StatsValueComparison Unsubscribes,
    StatsValueComparison UnsubscribeRate,
    StatsValueComparison SpamReports);

/// <summary>
/// Top and bottom regular emails of the range by one rate. Emails with fewer than
/// <see cref="EmailSummaryReportBuilder.MinDeliveredForRanking"/> delivered are left out.
/// </summary>
/// <param name="Top">Highest rate first.</param>
/// <param name="Bottom">Lowest rate first. Emails already in <paramref name="Top"/> are left out.</param>
public sealed record EmailSummaryPerformers(StatsRankedResult Top, StatsRankedResult Bottom);

/// <summary>
/// Email summary report.
/// </summary>
/// <param name="From">Applied range start (inclusive).</param>
/// <param name="To">Applied range end (inclusive).</param>
/// <param name="Grouping">Applied grouping.</param>
/// <param name="ChannelId">Applied email channel filter (channel ID), <c>null</c> for all email channels.</param>
/// <param name="Periods">Period axis of the activity series.</param>
/// <param name="Totals">KPIs of regular emails sent in the range vs the previous period.</param>
/// <param name="Sent">Sent records (hits) per period, all purposes.</param>
/// <param name="UniqueOpens">Recipients (mailouts) with an open or click per period, all purposes.</param>
/// <param name="UniqueClicks">Recipients (mailouts) with a click per period, all purposes.</param>
/// <param name="Unsubscribes">Recipients (mailouts) who unsubscribed per period, all purposes.</param>
/// <param name="Emails">Regular emails sent in the range, newest first.</param>
/// <param name="Automated">Automated emails with statistics (lifetime), most sent first.</param>
/// <param name="ByOpenRate">Top and bottom regular emails by open rate.</param>
/// <param name="ByClickRate">Top and bottom regular emails by click rate.</param>
/// <param name="Available"><c>false</c> when the email tables do not exist; the report is then empty.</param>
public sealed record EmailSummaryResult(
    DateOnly From,
    DateOnly To,
    StatsGrouping Grouping,
    int? ChannelId,
    IReadOnlyList<StatsPeriod> Periods,
    EmailSummaryTotals Totals,
    StatsValueSeries Sent,
    StatsValueSeries UniqueOpens,
    StatsValueSeries UniqueClicks,
    StatsValueSeries Unsubscribes,
    IReadOnlyList<EmailSummaryEmail> Emails,
    IReadOnlyList<EmailSummaryAutomatedEmail> Automated,
    EmailSummaryPerformers ByOpenRate,
    EmailSummaryPerformers ByClickRate,
    bool Available)
{
    /// <summary>
    /// Fewest delivered emails an email needs to be ranked as a top or bottom performer.
    /// </summary>
    public int MinDeliveredForRanking { get; init; } = EmailSummaryReportBuilder.MinDeliveredForRanking;

    /// <summary>
    /// Path of the native email list of the selected (or only) email channel, relative to the admin root.
    /// <c>null</c> with several email channels and no channel selected, or when it is not available.
    /// </summary>
    public string? EmailsAppPath { get; init; }

    /// <summary>
    /// When the data was read from the database. Can be older than the request when served from cache.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Email channel as returned by the SQL batch, for links to the native email application.
/// </summary>
/// <param name="EmailChannelId">Email channel ID (<c>EmailChannelInfo.EmailChannelID</c>, part of the native application slug).</param>
/// <param name="ChannelId">Channel ID (<c>ChannelInfo.ChannelID</c>, the ID in the channel filter).</param>
/// <param name="PrimaryLanguage">Code name of the channel's primary language.</param>
public sealed record EmailSummaryChannelRow(int EmailChannelId, int ChannelId, string? PrimaryLanguage);

/// <summary>
/// A regular email sent in the previous period or the range, as returned by the SQL batch.
/// </summary>
/// <param name="Id">Email configuration ID.</param>
/// <param name="CodeName">Code name (fallback name).</param>
/// <param name="DisplayName">Display name in the channel's primary language, else another language.</param>
/// <param name="EmailChannelId">Email channel ID, for the link.</param>
/// <param name="LanguageName">Language code name of <paramref name="DisplayName"/> (else the channel's primary language), for the link.</param>
/// <param name="SendTime">Send date as stored.</param>
/// <param name="RecipientList">Recipient list display name.</param>
/// <param name="Statistics">Lifetime numbers, <c>null</c> without a statistics row.</param>
public sealed record EmailSummaryEmailRow(
    int Id,
    string CodeName,
    string? DisplayName,
    int? EmailChannelId,
    string? LanguageName,
    DateTime SendTime,
    string? RecipientList,
    EmailStatisticsValues? Statistics);

/// <summary>
/// An automated email with a statistics row, as returned by the SQL batch.
/// </summary>
/// <param name="Id">Email configuration ID.</param>
/// <param name="CodeName">Code name.</param>
/// <param name="DisplayName">Display name.</param>
/// <param name="Purpose">Email purpose.</param>
/// <param name="EmailChannelId">Email channel ID.</param>
/// <param name="LanguageName">Language code name.</param>
/// <param name="SentInRange">Sent hits in the range.</param>
/// <param name="Statistics">Lifetime numbers.</param>
public sealed record EmailSummaryAutomatedRow(
    int Id,
    string CodeName,
    string? DisplayName,
    string Purpose,
    int? EmailChannelId,
    string? LanguageName,
    int SentInRange,
    EmailStatisticsValues Statistics);

/// <summary>
/// Hits of one period of the range (all purposes, channel filter applied), as returned by the SQL batch.
/// </summary>
/// <param name="PeriodStart">First day of the period (the range start for the first, partial period).</param>
/// <param name="Sent">Sent hits.</param>
/// <param name="UniqueOpens">Distinct mailouts with an open or click hit in the period.</param>
/// <param name="UniqueClicks">Distinct mailouts with a click hit in the period.</param>
/// <param name="Unsubscribes">Distinct mailouts with an unsubscribe hit in the period.</param>
public sealed record EmailSummaryActivityRow(DateOnly PeriodStart, int Sent, int UniqueOpens, int UniqueClicks, int Unsubscribes);

/// <summary>
/// Aggregated email data. <see cref="Emails"/> starts at the previous period; <see cref="Activity"/> covers the range only.
/// </summary>
/// <param name="Available"><c>false</c> when the email tables do not exist.</param>
/// <param name="Channels">Email channels.</param>
/// <param name="Emails">Regular emails sent in the previous period or the range.</param>
/// <param name="Automated">Automated emails with statistics.</param>
/// <param name="Activity">Hits per period of the range.</param>
public sealed record EmailSummaryReportData(
    bool Available,
    IReadOnlyList<EmailSummaryChannelRow> Channels,
    IReadOnlyList<EmailSummaryEmailRow> Emails,
    IReadOnlyList<EmailSummaryAutomatedRow> Automated,
    IReadOnlyList<EmailSummaryActivityRow> Activity)
{
    public static EmailSummaryReportData Empty { get; } = new(true, [], [], [], []);

    public static EmailSummaryReportData Unavailable { get; } = Empty with { Available = false };
}

/// <summary>
/// Email data with the time it was read. This is the cached value.
/// </summary>
internal sealed record EmailSummarySnapshot(EmailSummaryReportData Data, DateTimeOffset ReadAt);
