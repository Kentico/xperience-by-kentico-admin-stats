using CMS.EmailLibrary;
using CMS.EmailMarketing;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EmailSummary;

/// <summary>
/// Builds the email summary batch. Only constant SQL fragments are combined (product enum values included as numbers); all values are parameters.
/// Tables and columns are those of <c>CMS.EmailLibrary.EmailConfigurationInfo</c> (<c>EmailLibrary_EmailConfiguration</c>),
/// <c>EmailChannelInfo</c> (<c>EmailLibrary_EmailChannel</c>), <c>EmailStatisticsInfo</c> (<c>EmailLibrary_EmailStatistics</c>),
/// <c>EmailStatisticsHitsInfo</c> (<c>EmailLibrary_EmailStatisticsHits</c>), <c>CMS.EmailMarketing.SendConfigurationInfo</c>
/// (<c>EmailLibrary_SendConfiguration</c>), <c>CMS.ContentEngine.ContentItemLanguageMetadataInfo</c> (<c>CMS_ContentItemLanguageMetadata</c>),
/// <c>ContentLanguageInfo</c> (<c>CMS_ContentLanguage</c>) and <c>CMS.ContactManagement.ContactGroupInfo</c> (<c>OM_ContactGroup</c>).
/// </summary>
/// <remarks>
/// <para>
/// <c>EmailStatistics</c> has one row per email with lifetime totals and no dates; these are the numbers of the native Statistics tab.
/// The product's recalculation adds totals up from unprocessed hits and recalculates uniques from all hits. Delivery providers can write
/// bounces, spam reports and delivered directly into the table without hits, so they are always read from the table, never from hits.
/// </para>
/// <para>
/// Hits (<c>EmailStatisticsHits</c>) are raw events with a time (server local time, compared as stored). The activity series counts sent hits
/// and distinct mailouts (one per recipient send) per period: unique opens = an open or click hit (the product's rule), unique clicks = a click
/// hit, unsubscribes = an unsubscribe hit. Periods are computed here (<see cref="GroupingParameter"/>), so uniques are per period, not per day.
/// </para>
/// <para>
/// The channel filter (<see cref="ChannelIdParameter"/>, a <c>CMS_Channel</c> ID, 0 for all) applies through the email's email channel.
/// The batch starts with one row (<see cref="AvailableColumn"/>): <c>0</c> when a table does not exist, and then returns nothing else.
/// Then, in order:
/// </para>
/// <list type="number">
/// <item>Email channels (all, for links).</item>
/// <item>Regular emails sent (status Sending or Sent) with a send date from the previous period start to the range end, newest first.</item>
/// <item>Automated emails (every other purpose) with a statistics row, with sent hits in the range, most sent first.</item>
/// <item>Hits per period of the range.</item>
/// </list>
/// </remarks>
internal static class EmailSummarySql
{
    /// <summary>Start of the previous period (inclusive).</summary>
    public const string PreviousFromParameter = "@PreviousFrom";

    /// <summary>Start of the range (inclusive).</summary>
    public const string FromParameter = "@From";

    /// <summary>Day after the end of the range (exclusive).</summary>
    public const string ToExclusiveParameter = "@ToExclusive";

    /// <summary>Channel ID (<c>CMS_Channel</c>) of the channel filter, 0 for all email channels.</summary>
    public const string ChannelIdParameter = "@ChannelId";

    /// <summary>Grouping of the activity series (<see cref="StatsGrouping"/> as a number: 0 day, 1 week, 2 month).</summary>
    public const string GroupingParameter = "@Grouping";

    /// <summary>Purpose of regular emails (<see cref="EmailPurpose.Regular"/> as stored).</summary>
    public const string RegularPurposeParameter = "@RegularPurpose";

    public const string AvailableColumn = "EmailSummaryAvailable";
    public const string EmailChannelIdColumn = "EmailChannelID";
    public const string ChannelIdColumn = "EmailChannelChannelID";
    public const string PrimaryLanguageColumn = "PrimaryLanguage";
    public const string EmailIdColumn = "EmailConfigurationID";
    public const string CodeNameColumn = "EmailConfigurationName";
    public const string DisplayNameColumn = "DisplayName";
    public const string LanguageColumn = "LanguageName";
    public const string PurposeColumn = "EmailConfigurationPurpose";
    public const string SendTimeColumn = "SendConfigurationScheduledTime";
    public const string RecipientListColumn = "RecipientList";
    public const string HasStatisticsColumn = "HasStatistics";
    public const string SentColumn = "EmailStatisticsTotalSent";
    public const string DeliveredColumn = "EmailStatisticsEmailsDelivered";
    public const string UniqueOpensColumn = "EmailStatisticsEmailUniqueOpens";
    public const string UniqueClicksColumn = "EmailStatisticsEmailUniqueClicks";
    public const string SoftBouncesColumn = "EmailStatisticsEmailSoftBounces";
    public const string HardBouncesColumn = "EmailStatisticsEmailHardBounces";
    public const string UnsubscribesColumn = "EmailStatisticsUniqueUnsubscribes";
    public const string SpamReportsColumn = "EmailStatisticsSpamReports";
    public const string SentInRangeColumn = "SentInRange";
    public const string PeriodStartColumn = "PeriodStart";
    public const string ActivitySentColumn = "Sent";
    public const string ActivityOpensColumn = "UniqueOpens";
    public const string ActivityClicksColumn = "UniqueClicks";
    public const string ActivityUnsubscribesColumn = "Unsubscribes";

    private const int SentHit = (int)EmailStatisticsHitsType.Sent;
    private const int OpenHit = (int)EmailStatisticsHitsType.Open;
    private const int ClickHit = (int)EmailStatisticsHitsType.Click;
    private const int UnsubscribeHit = (int)EmailStatisticsHitsType.Unsubscribe;
    private const int SendingStatus = (int)SendConfigurationStatus.Sending;
    private const int SentStatus = (int)SendConfigurationStatus.Sent;

    private static readonly string availabilityCheck = StatsSql.BuildAvailabilityCheck(
        AvailableColumn,
        "EmailLibrary_EmailConfiguration",
        "EmailLibrary_EmailChannel",
        "EmailLibrary_EmailStatistics",
        "EmailLibrary_EmailStatisticsHits",
        "EmailLibrary_SendConfiguration",
        "CMS_ContentItemLanguageMetadata",
        "CMS_ContentLanguage",
        "OM_ContactGroup");

    /// <summary>
    /// Emails of the channel filter with their display name: the channel's primary language first, else the language with the lowest
    /// metadata ID (the native email list shows the content item's language metadata display name).
    /// </summary>
    private const string EmailsQuery = """
        DECLARE @Emails TABLE (
            [EmailID] int PRIMARY KEY,
            [CodeName] nvarchar(200) NOT NULL,
            [Purpose] nvarchar(100) NOT NULL,
            [EmailChannelID] int NULL,
            [DisplayName] nvarchar(200) NULL,
            [LanguageName] nvarchar(100) NULL);

        INSERT INTO @Emails
        SELECT
            E.[EmailConfigurationID],
            E.[EmailConfigurationName],
            E.[EmailConfigurationPurpose],
            C.[EmailChannelID],
            M.[ContentItemLanguageMetadataDisplayName],
            COALESCE(M.[ContentLanguageName], P.[ContentLanguageName])
        FROM [EmailLibrary_EmailConfiguration] E
        LEFT JOIN [EmailLibrary_EmailChannel] C ON C.[EmailChannelID] = E.[EmailConfigurationEmailChannelID]
        LEFT JOIN [CMS_ContentLanguage] P ON P.[ContentLanguageID] = C.[EmailChannelPrimaryContentLanguageID]
        OUTER APPLY (
            SELECT TOP (1) LM.[ContentItemLanguageMetadataDisplayName], L.[ContentLanguageName]
            FROM [CMS_ContentItemLanguageMetadata] LM
            INNER JOIN [CMS_ContentLanguage] L ON L.[ContentLanguageID] = LM.[ContentItemLanguageMetadataContentLanguageID]
            WHERE LM.[ContentItemLanguageMetadataContentItemID] = E.[EmailConfigurationContentItemID]
            ORDER BY
                CASE WHEN LM.[ContentItemLanguageMetadataContentLanguageID] = C.[EmailChannelPrimaryContentLanguageID] THEN 0 ELSE 1 END,
                LM.[ContentItemLanguageMetadataID]
        ) M
        WHERE @ChannelId = 0 OR C.[EmailChannelChannelID] = @ChannelId;
        """;

    // 1. Email channels.
    private const string ChannelsQuery = """
        SELECT C.[EmailChannelID], C.[EmailChannelChannelID], L.[ContentLanguageName] AS [PrimaryLanguage]
        FROM [EmailLibrary_EmailChannel] C
        LEFT JOIN [CMS_ContentLanguage] L ON L.[ContentLanguageID] = C.[EmailChannelPrimaryContentLanguageID]
        ORDER BY C.[EmailChannelID];
        """;

    // Lifetime statistics of an email (one row per email; the newest row if there are more).
    private const string StatisticsApply = """
        SELECT TOP (1)
            S.[EmailStatisticsTotalSent],
            S.[EmailStatisticsEmailsDelivered],
            S.[EmailStatisticsEmailUniqueOpens],
            S.[EmailStatisticsEmailUniqueClicks],
            S.[EmailStatisticsEmailSoftBounces],
            S.[EmailStatisticsEmailHardBounces],
            S.[EmailStatisticsUniqueUnsubscribes],
            S.[EmailStatisticsSpamReports]
        FROM [EmailLibrary_EmailStatistics] S
        WHERE S.[EmailStatisticsEmailConfigurationID] = E.[EmailID]
        ORDER BY S.[EmailStatisticsID] DESC
        """;

    private const string StatisticsColumns = """
            ST.[EmailStatisticsTotalSent],
            ST.[EmailStatisticsEmailsDelivered],
            ST.[EmailStatisticsEmailUniqueOpens],
            ST.[EmailStatisticsEmailUniqueClicks],
            ST.[EmailStatisticsEmailSoftBounces],
            ST.[EmailStatisticsEmailHardBounces],
            ST.[EmailStatisticsUniqueUnsubscribes],
            ST.[EmailStatisticsSpamReports]
        """;

    // 2. Regular emails sent in the previous period or the range.
    private static readonly string regularQuery = $"""
        SELECT
            E.[EmailID] AS [EmailConfigurationID],
            E.[CodeName] AS [EmailConfigurationName],
            E.[DisplayName],
            E.[EmailChannelID],
            E.[LanguageName],
            SC.[SendConfigurationScheduledTime],
            G.[ContactGroupDisplayName] AS [RecipientList],
            CAST(CASE WHEN ST.[EmailStatisticsTotalSent] IS NULL THEN 0 ELSE 1 END AS bit) AS [HasStatistics],
        {StatisticsColumns}
        FROM @Emails E
        INNER JOIN [EmailLibrary_SendConfiguration] SC ON SC.[SendConfigurationEmailConfigurationID] = E.[EmailID]
        LEFT JOIN [OM_ContactGroup] G ON G.[ContactGroupID] = SC.[SendConfigurationRecipientListID]
        OUTER APPLY (
        {StatisticsApply}
        ) ST
        WHERE E.[Purpose] = @RegularPurpose
            AND SC.[SendConfigurationStatus] IN ({SendingStatus}, {SentStatus})
            AND SC.[SendConfigurationScheduledTime] >= @PreviousFrom
            AND SC.[SendConfigurationScheduledTime] < @ToExclusive
        ORDER BY SC.[SendConfigurationScheduledTime] DESC, E.[EmailID];
        """;

    // 3. Automated emails with statistics (lifetime) and sent hits in the range.
    private static readonly string automatedQuery = $"""
        SELECT
            E.[EmailID] AS [EmailConfigurationID],
            E.[CodeName] AS [EmailConfigurationName],
            E.[DisplayName],
            E.[Purpose] AS [EmailConfigurationPurpose],
            E.[EmailChannelID],
            E.[LanguageName],
            H.[SentInRange],
        {StatisticsColumns}
        FROM @Emails E
        CROSS APPLY (
        {StatisticsApply}
        ) ST
        CROSS APPLY (
            SELECT COUNT(*) AS [SentInRange]
            FROM [EmailLibrary_EmailStatisticsHits] HT
            WHERE HT.[EmailStatisticsHitsEmailConfigurationID] = E.[EmailID]
                AND HT.[EmailStatisticsHitsType] = {SentHit}
                AND HT.[EmailStatisticsHitsTime] >= @From
                AND HT.[EmailStatisticsHitsTime] < @ToExclusive
        ) H
        WHERE E.[Purpose] <> @RegularPurpose
        ORDER BY ST.[EmailStatisticsTotalSent] DESC, E.[EmailID];
        """;

    // 4. Hits per period of the range. Weeks start on Monday (1900-01-01 was a Monday); the first period starts at the range start.
    private static readonly string activityQuery = $"""
        SELECT
            CASE WHEN P.[Start] < CAST(@From AS date) THEN CAST(@From AS date) ELSE P.[Start] END AS [PeriodStart],
            SUM(CASE WHEN H.[EmailStatisticsHitsType] = {SentHit} THEN 1 ELSE 0 END) AS [Sent],
            COUNT(DISTINCT CASE WHEN H.[EmailStatisticsHitsType] IN ({OpenHit}, {ClickHit}) THEN H.[EmailStatisticsHitsMailoutGUID] END) AS [UniqueOpens],
            COUNT(DISTINCT CASE WHEN H.[EmailStatisticsHitsType] = {ClickHit} THEN H.[EmailStatisticsHitsMailoutGUID] END) AS [UniqueClicks],
            COUNT(DISTINCT CASE WHEN H.[EmailStatisticsHitsType] = {UnsubscribeHit} THEN H.[EmailStatisticsHitsMailoutGUID] END) AS [Unsubscribes]
        FROM [EmailLibrary_EmailStatisticsHits] H
        INNER JOIN @Emails E ON E.[EmailID] = H.[EmailStatisticsHitsEmailConfigurationID]
        CROSS APPLY (SELECT CAST(H.[EmailStatisticsHitsTime] AS date) AS [Day]) D
        CROSS APPLY (SELECT CASE @Grouping
            WHEN 1 THEN DATEADD(day, -(DATEDIFF(day, '19000101', D.[Day]) % 7), D.[Day])
            WHEN 2 THEN DATEFROMPARTS(YEAR(D.[Day]), MONTH(D.[Day]), 1)
            ELSE D.[Day] END AS [Start]) P
        WHERE H.[EmailStatisticsHitsTime] >= @From
            AND H.[EmailStatisticsHitsTime] < @ToExclusive
            AND H.[EmailStatisticsHitsType] IN ({SentHit}, {OpenHit}, {ClickHit}, {UnsubscribeHit})
        GROUP BY CASE WHEN P.[Start] < CAST(@From AS date) THEN CAST(@From AS date) ELSE P.[Start] END
        ORDER BY [PeriodStart];
        """;

    /// <summary>
    /// Builds the batch (see the class remarks).
    /// </summary>
    public static string BuildReport() =>
        string.Join(Environment.NewLine, "SET NOCOUNT ON;", availabilityCheck, EmailsQuery, ChannelsQuery, regularQuery, automatedQuery, activityQuery);
}
