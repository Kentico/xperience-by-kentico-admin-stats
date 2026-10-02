using System.Data;
using System.Data.Common;

using CMS.DataEngine;
using CMS.EmailLibrary;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EmailSummary;

/// <summary>
/// Reads aggregated email data from the database.
/// </summary>
internal interface IEmailSummaryRepository
{
    /// <summary>
    /// Returns email channels, regular emails sent from <paramref name="previousFrom"/> to <paramref name="to"/>, automated emails with statistics
    /// and hits per period of the range. <see cref="EmailSummaryReportData.Unavailable"/> when the tables do not exist.
    /// </summary>
    /// <param name="previousFrom">First day of the previous period (inclusive).</param>
    /// <param name="from">First day of the range (inclusive).</param>
    /// <param name="to">Last day of the range (inclusive).</param>
    /// <param name="channelId">Channel filter (channel ID), <c>null</c> for all email channels.</param>
    /// <param name="grouping">Period size of the activity rows.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<EmailSummaryReportData> GetData(
        DateOnly previousFrom,
        DateOnly from,
        DateOnly to,
        int? channelId,
        StatsGrouping grouping,
        CancellationToken cancellationToken);
}

internal sealed class EmailSummaryRepository : IEmailSummaryRepository
{
    private const string ReportName = "email summary";

    public async Task<EmailSummaryReportData> GetData(
        DateOnly previousFrom,
        DateOnly from,
        DateOnly to,
        int? channelId,
        StatsGrouping grouping,
        CancellationToken cancellationToken)
    {
        var parameters = new QueryDataParameters
        {
            new DataParameter(EmailSummarySql.PreviousFromParameter, previousFrom.ToDateTime(TimeOnly.MinValue)),
            new DataParameter(EmailSummarySql.FromParameter, from.ToDateTime(TimeOnly.MinValue)),
            new DataParameter(EmailSummarySql.ToExclusiveParameter, to.AddDays(1).ToDateTime(TimeOnly.MinValue)),
            new DataParameter(EmailSummarySql.ChannelIdParameter, channelId ?? 0),
            new DataParameter(EmailSummarySql.GroupingParameter, (int)grouping),
            new DataParameter(EmailSummarySql.RegularPurposeParameter, EmailPurpose.Regular.ToString()),
        };

        await using var reader = await ConnectionHelper.ExecuteReaderAsync(
            EmailSummarySql.BuildReport(),
            parameters,
            QueryTypeEnum.SQLQuery,
            CommandBehavior.Default,
            cancellationToken);

        if (!await StatsSql.IsAvailable(reader, EmailSummarySql.AvailableColumn, cancellationToken))
        {
            return EmailSummaryReportData.Unavailable;
        }

        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var channels = await ReadChannels(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var emails = await ReadEmails(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var automated = await ReadAutomated(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var activity = await ReadActivity(reader, cancellationToken);

        return new(true, channels, emails, automated, activity);
    }

    private static async Task<IReadOnlyList<EmailSummaryChannelRow>> ReadChannels(DbDataReader reader, CancellationToken cancellationToken)
    {
        int emailChannelOrdinal = reader.GetOrdinal(EmailSummarySql.EmailChannelIdColumn);
        int channelOrdinal = reader.GetOrdinal(EmailSummarySql.ChannelIdColumn);
        int languageOrdinal = reader.GetOrdinal(EmailSummarySql.PrimaryLanguageColumn);

        var rows = new List<EmailSummaryChannelRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(reader.GetInt32(emailChannelOrdinal), reader.GetInt32(channelOrdinal), GetString(reader, languageOrdinal)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<EmailSummaryEmailRow>> ReadEmails(DbDataReader reader, CancellationToken cancellationToken)
    {
        var email = EmailOrdinals.Read(reader);
        var statistics = StatisticsOrdinals.Read(reader);
        int sendTimeOrdinal = reader.GetOrdinal(EmailSummarySql.SendTimeColumn);
        int recipientListOrdinal = reader.GetOrdinal(EmailSummarySql.RecipientListColumn);
        int hasStatisticsOrdinal = reader.GetOrdinal(EmailSummarySql.HasStatisticsColumn);

        var rows = new List<EmailSummaryEmailRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.GetInt32(email.Id),
                reader.GetString(email.CodeName),
                GetString(reader, email.DisplayName),
                GetInt(reader, email.EmailChannelId),
                GetString(reader, email.Language),
                reader.GetDateTime(sendTimeOrdinal),
                GetString(reader, recipientListOrdinal),
                reader.GetBoolean(hasStatisticsOrdinal) ? statistics.Get(reader) : null));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<EmailSummaryAutomatedRow>> ReadAutomated(DbDataReader reader, CancellationToken cancellationToken)
    {
        var email = EmailOrdinals.Read(reader);
        var statistics = StatisticsOrdinals.Read(reader);
        int purposeOrdinal = reader.GetOrdinal(EmailSummarySql.PurposeColumn);
        int sentInRangeOrdinal = reader.GetOrdinal(EmailSummarySql.SentInRangeColumn);

        var rows = new List<EmailSummaryAutomatedRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.GetInt32(email.Id),
                reader.GetString(email.CodeName),
                GetString(reader, email.DisplayName),
                reader.GetString(purposeOrdinal),
                GetInt(reader, email.EmailChannelId),
                GetString(reader, email.Language),
                reader.GetInt32(sentInRangeOrdinal),
                statistics.Get(reader)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<EmailSummaryActivityRow>> ReadActivity(DbDataReader reader, CancellationToken cancellationToken)
    {
        int periodOrdinal = reader.GetOrdinal(EmailSummarySql.PeriodStartColumn);
        int sentOrdinal = reader.GetOrdinal(EmailSummarySql.ActivitySentColumn);
        int opensOrdinal = reader.GetOrdinal(EmailSummarySql.ActivityOpensColumn);
        int clicksOrdinal = reader.GetOrdinal(EmailSummarySql.ActivityClicksColumn);
        int unsubscribesOrdinal = reader.GetOrdinal(EmailSummarySql.ActivityUnsubscribesColumn);

        var rows = new List<EmailSummaryActivityRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                DateOnly.FromDateTime(reader.GetDateTime(periodOrdinal)),
                reader.GetInt32(sentOrdinal),
                reader.GetInt32(opensOrdinal),
                reader.GetInt32(clicksOrdinal),
                reader.GetInt32(unsubscribesOrdinal)));
        }

        return rows;
    }

    private static string? GetString(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static int? GetInt(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);

    private sealed record EmailOrdinals(int Id, int CodeName, int DisplayName, int EmailChannelId, int Language)
    {
        public static EmailOrdinals Read(DbDataReader reader) =>
            new(
                reader.GetOrdinal(EmailSummarySql.EmailIdColumn),
                reader.GetOrdinal(EmailSummarySql.CodeNameColumn),
                reader.GetOrdinal(EmailSummarySql.DisplayNameColumn),
                reader.GetOrdinal(EmailSummarySql.EmailChannelIdColumn),
                reader.GetOrdinal(EmailSummarySql.LanguageColumn));
    }

    private sealed record StatisticsOrdinals(
        int Sent,
        int Delivered,
        int UniqueOpens,
        int UniqueClicks,
        int SoftBounces,
        int HardBounces,
        int Unsubscribes,
        int SpamReports)
    {
        public static StatisticsOrdinals Read(DbDataReader reader) =>
            new(
                reader.GetOrdinal(EmailSummarySql.SentColumn),
                reader.GetOrdinal(EmailSummarySql.DeliveredColumn),
                reader.GetOrdinal(EmailSummarySql.UniqueOpensColumn),
                reader.GetOrdinal(EmailSummarySql.UniqueClicksColumn),
                reader.GetOrdinal(EmailSummarySql.SoftBouncesColumn),
                reader.GetOrdinal(EmailSummarySql.HardBouncesColumn),
                reader.GetOrdinal(EmailSummarySql.UnsubscribesColumn),
                reader.GetOrdinal(EmailSummarySql.SpamReportsColumn));

        public EmailStatisticsValues Get(DbDataReader reader) =>
            new(
                reader.GetInt32(Sent),
                reader.GetInt32(Delivered),
                reader.GetInt32(UniqueOpens),
                reader.GetInt32(UniqueClicks),
                GetInt(reader, SoftBounces),
                GetInt(reader, HardBounces),
                reader.GetInt32(Unsubscribes),
                GetInt(reader, SpamReports));
    }
}
