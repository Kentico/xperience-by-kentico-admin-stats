using System.Globalization;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EmailSummary;

/// <summary>
/// Returns the path of an email's Statistics tab, or <c>null</c>.
/// </summary>
/// <param name="emailId">Email configuration ID.</param>
/// <param name="emailChannelId">Email channel ID (<c>EmailChannelInfo.EmailChannelID</c>), <c>null</c> when unknown.</param>
/// <param name="languageName">Language code name, <c>null</c> when unknown.</param>
internal delegate string? EmailStatisticsPathProvider(int emailId, int? emailChannelId, string? languageName);

/// <summary>
/// Turns aggregated email data into the email summary report.
/// </summary>
/// <remarks>
/// KPIs, the email table and the top / bottom performers cover regular emails only: they are marketing emails sent to a recipient list
/// with double opt-in. Other purposes (automation, form autoresponders, confirmations, commerce) mix transactional and marketing sends,
/// so they are listed separately with lifetime numbers.
/// </remarks>
internal static class EmailSummaryReportBuilder
{
    /// <summary>
    /// Fewest delivered emails an email needs to be ranked as a top or bottom performer, so tiny sends (tests, small lists)
    /// do not top the list with 100%.
    /// </summary>
    public const int MinDeliveredForRanking = 10;

    /// <summary>
    /// Number of top and of bottom performers.
    /// </summary>
    public const int PerformerCount = 5;

    public static StatsSeriesDefinition SentSeries { get; } = new("sent", "Sent");

    public static StatsSeriesDefinition UniqueOpensSeries { get; } = new("unique-opens", "Unique opens");

    public static StatsSeriesDefinition UniqueClicksSeries { get; } = new("unique-clicks", "Unique clicks");

    public static StatsSeriesDefinition UnsubscribesSeries { get; } = new("unsubscribes", "Unsubscribes");

    /// <summary>
    /// Builds the report.
    /// </summary>
    /// <param name="query">Normalized filter. A channel that is not an email channel in <see cref="EmailSummaryReportData.Channels"/> means all.</param>
    /// <param name="data">Emails sent from the start of the previous period (see <see cref="StatsComparison.GetPreviousRange"/>) to the end of the range.</param>
    /// <param name="getStatisticsPath">Returns the path of an email's Statistics tab, or <c>null</c>.</param>
    public static EmailSummaryResult Build(StatsQuery query, EmailSummaryReportData data, EmailStatisticsPathProvider? getStatisticsPath = null)
    {
        var (previousFrom, previousTo) = StatsComparison.GetPreviousRange(query);
        int? channelId = query.ChannelId is int id && data.Channels.Any(c => c.ChannelId == id) ? id : null;

        var current = data.Emails.Where(row => IsIn(row.SendTime, query.From, query.To)).ToList();
        var previous = data.Emails.Where(row => IsIn(row.SendTime, previousFrom, previousTo)).ToList();

        var emails = current
            .Select(row =>
            {
                var statistics = Clamp(row.Statistics ?? EmailStatisticsValues.Zero);
                return new EmailSummaryEmail(
                    row.Id,
                    GetName(row.DisplayName, row.CodeName),
                    row.SendTime,
                    string.IsNullOrWhiteSpace(row.RecipientList) ? null : row.RecipientList.Trim(),
                    statistics,
                    row.Statistics is not null,
                    EmailRates.From(statistics))
                {
                    StatisticsPath = getStatisticsPath?.Invoke(row.Id, row.EmailChannelId, row.LanguageName),
                };
            })
            .OrderByDescending(email => email.SendTime)
            .ThenBy(email => email.Id)
            .ToList();

        var automated = data.Automated
            .Select(row =>
            {
                var statistics = Clamp(row.Statistics);
                return new EmailSummaryAutomatedEmail(
                    row.Id,
                    GetName(row.DisplayName, row.CodeName),
                    row.Purpose,
                    Math.Max(row.SentInRange, 0),
                    statistics,
                    EmailRates.From(statistics))
                {
                    StatisticsPath = getStatisticsPath?.Invoke(row.Id, row.EmailChannelId, row.LanguageName),
                };
            })
            .OrderByDescending(email => email.Statistics.Sent)
            .ThenBy(email => email.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(email => email.Id)
            .ToList();

        StatsValueSeries Series(StatsSeriesDefinition definition, Func<EmailSummaryActivityRow, int> value) =>
            StatsTimeSeriesBuilder.BuildValueSeries(
                query,
                data.Activity.Select(row => new StatsDailyValue(definition.Key, row.PeriodStart, value(row))),
                definition,
                StatsValueKind.Count);

        return new(
            query.From,
            query.To,
            query.Grouping,
            channelId,
            StatsPeriods.Build(query.From, query.To, query.Grouping),
            BuildTotals(query, Sum(current), Sum(previous)),
            Series(SentSeries, row => row.Sent),
            Series(UniqueOpensSeries, row => row.UniqueOpens),
            Series(UniqueClicksSeries, row => row.UniqueClicks),
            Series(UnsubscribesSeries, row => row.Unsubscribes),
            emails,
            automated,
            BuildPerformers(query, channelId, emails, rates => rates.OpenRate),
            BuildPerformers(query, channelId, emails, rates => rates.ClickRate),
            data.Available);
    }

    /// <summary>
    /// Email channel of the native email list link: the selected channel, else the only email channel. <c>null</c> with several channels
    /// and no channel selected.
    /// </summary>
    public static EmailSummaryChannelRow? GetListChannel(int? channelId, EmailSummaryReportData data)
    {
        var selected = channelId is int id ? data.Channels.FirstOrDefault(c => c.ChannelId == id) : null;
        return selected ?? (data.Channels.Count == 1 ? data.Channels[0] : null);
    }

    /// <summary>
    /// Display name, else the code name.
    /// </summary>
    public static string GetName(string? displayName, string codeName) =>
        string.IsNullOrWhiteSpace(displayName) ? codeName : displayName.Trim();

    private static bool IsIn(DateTime time, DateOnly from, DateOnly to)
    {
        var day = DateOnly.FromDateTime(time);
        return day >= from && day <= to;
    }

    private sealed record Sums(
        int Emails,
        EmailStatisticsValues Values);

    /// <summary>
    /// Sums of the emails. Bounces and spam reports are summed over the emails that have them, and are <c>null</c> when none has them.
    /// </summary>
    private static Sums Sum(IReadOnlyList<EmailSummaryEmailRow> rows)
    {
        var values = rows.Select(row => Clamp(row.Statistics ?? EmailStatisticsValues.Zero)).ToList();

        int? SumNullable(Func<EmailStatisticsValues, int?> value) =>
            values.Any(v => value(v) is not null) ? values.Sum(v => value(v) ?? 0) : null;

        return new(
            rows.Count,
            new(
                values.Sum(v => v.Sent),
                values.Sum(v => v.Delivered),
                values.Sum(v => v.UniqueOpens),
                values.Sum(v => v.UniqueClicks),
                SumNullable(v => v.SoftBounces),
                SumNullable(v => v.HardBounces),
                values.Sum(v => v.Unsubscribes),
                SumNullable(v => v.SpamReports)));
    }

    private static EmailSummaryTotals BuildTotals(StatsQuery query, Sums current, Sums previous)
    {
        var currentRates = EmailRates.From(current.Values);
        var previousRates = EmailRates.From(previous.Values);

        StatsValueComparison Count(int? currentValue, int? previousValue) =>
            StatsValueComparison.Create(query, StatsValueKind.Count, currentValue, previousValue);

        StatsValueComparison Ratio(Func<EmailRates, decimal?> rate) =>
            StatsValueComparison.Create(query, StatsValueKind.Ratio, rate(currentRates), rate(previousRates));

        return new(
            Count(current.Emails, previous.Emails),
            Count(current.Values.Sent, previous.Values.Sent),
            Count(current.Values.Delivered, previous.Values.Delivered),
            Ratio(rates => rates.DeliveryRate),
            Ratio(rates => rates.OpenRate),
            Ratio(rates => rates.ClickRate),
            Count(current.Values.HardBounces, previous.Values.HardBounces),
            Count(current.Values.SoftBounces, previous.Values.SoftBounces),
            Count(current.Values.Unsubscribes, previous.Values.Unsubscribes),
            Ratio(rates => rates.UnsubscribeRate),
            Count(current.Values.SpamReports, previous.Values.SpamReports));
    }

    /// <summary>
    /// Top and bottom emails by a rate. Emails with fewer than <see cref="MinDeliveredForRanking"/> delivered are left out.
    /// Ties: more delivered first, then by name. The bottom list leaves out emails already in the top list.
    /// </summary>
    private static EmailSummaryPerformers BuildPerformers(
        StatsQuery query,
        int? channelId,
        IReadOnlyList<EmailSummaryEmail> emails,
        Func<EmailRates, decimal?> getRate)
    {
        var eligible = emails
            .Where(email => email.Statistics.Delivered >= MinDeliveredForRanking && getRate(email.Rates) is not null)
            .ToList();

        var top = eligible
            .OrderByDescending(email => getRate(email.Rates))
            .ThenByDescending(email => email.Statistics.Delivered)
            .ThenBy(email => email.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(email => email.Id)
            .Take(PerformerCount)
            .ToList();

        var bottom = eligible
            .Except(top)
            .OrderBy(email => getRate(email.Rates))
            .ThenByDescending(email => email.Statistics.Delivered)
            .ThenBy(email => email.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(email => email.Id)
            .Take(PerformerCount)
            .ToList();

        StatsRankedResult Ranked(IReadOnlyList<EmailSummaryEmail> list) =>
            StatsRankedBuilder.Build(
                query with { ChannelId = channelId },
                list.Select(email => new StatsRankedEntry(
                    email.Id.ToString(CultureInfo.InvariantCulture),
                    email.Name,
                    email.SendTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    getRate(email.Rates) ?? 0,
                    email.Statistics.Delivered,
                    null)
                {
                    AdminPath = email.StatisticsPath,
                }),
                total: 0,
                itemCount: eligible.Count,
                limit: PerformerCount,
                includeZero: true,
                keepOrder: true) with
            {
                ValueKind = StatsValueKind.Ratio,
            };

        return new(Ranked(top), Ranked(bottom));
    }

    private static EmailStatisticsValues Clamp(EmailStatisticsValues values) =>
        new(
            Math.Max(values.Sent, 0),
            Math.Max(values.Delivered, 0),
            Math.Max(values.UniqueOpens, 0),
            Math.Max(values.UniqueClicks, 0),
            values.SoftBounces is int soft ? Math.Max(soft, 0) : null,
            values.HardBounces is int hard ? Math.Max(hard, 0) : null,
            Math.Max(values.Unsubscribes, 0),
            values.SpamReports is int spam ? Math.Max(spam, 0) : null);
}
