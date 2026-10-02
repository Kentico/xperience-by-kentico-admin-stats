using CMS.ContactManagement;
using CMS.ContentEngine;
using CMS.EmailLibrary;
using CMS.EmailMarketing;

using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EmailSummary;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

/// <summary>
/// The batch was also run against the local DancingGoat database (seeded and real sends) and read-only against a copy of the Community Portal
/// database, where unique opens / clicks / unsubscribes from hits (per mailout, a click counts as an open) match the statistics table for every
/// email with hits. These tests pin the SQL shape that makes those cases right.
/// </summary>
public class EmailSummarySqlTests
{
    private static readonly string sql = EmailSummarySql.BuildReport().ReplaceLineEndings("\n");

    [Test]
    public void BuildReport_ChecksTablesFirst_AndReturnsEarly()
    {
        string[] tables =
        [
            "EmailLibrary_EmailConfiguration",
            "EmailLibrary_EmailChannel",
            "EmailLibrary_EmailStatistics",
            "EmailLibrary_EmailStatisticsHits",
            "EmailLibrary_SendConfiguration",
            "CMS_ContentItemLanguageMetadata",
            "CMS_ContentLanguage",
            "OM_ContactGroup",
        ];
        Assert.That(tables, Is.All.Matches<string>(table => sql.Contains($"OBJECT_ID(N'[{table}]', N'U') IS NULL", StringComparison.Ordinal)));
        Assert.That(sql, Does.Contain($"SELECT CAST(0 AS bit) AS [{EmailSummarySql.AvailableColumn}];"));
        Assert.That(
            sql.IndexOf("RETURN;", StringComparison.Ordinal),
            Is.LessThan(sql.IndexOf("FROM [EmailLibrary_EmailConfiguration] E", StringComparison.Ordinal)));
    }

    [Test]
    public void BuildReport_UsesParameters()
    {
        Assert.That(sql, Does.Contain("WHERE @ChannelId = 0 OR C.[EmailChannelChannelID] = @ChannelId;"));
        Assert.That(sql, Does.Contain("SC.[SendConfigurationScheduledTime] >= @PreviousFrom"));
        Assert.That(sql, Does.Contain("SC.[SendConfigurationScheduledTime] < @ToExclusive"));
        Assert.That(sql, Does.Contain("HT.[EmailStatisticsHitsTime] >= @From"));
        Assert.That(sql, Does.Contain("H.[EmailStatisticsHitsTime] < @ToExclusive"));
        Assert.That(sql, Does.Contain("E.[Purpose] = @RegularPurpose"));
        Assert.That(sql, Does.Contain("E.[Purpose] <> @RegularPurpose"));
        Assert.That(sql, Does.Contain("CASE @Grouping"));
    }

    [Test]
    public void BuildReport_RegularEmails_SentOrSendingOnly()
    {
        // Draft (0) and Scheduled (1) are left out.
        Assert.That(sql, Does.Contain("SC.[SendConfigurationStatus] IN (2, 3)"));
        Assert.That((int)SendConfigurationStatus.Sending, Is.EqualTo(2));
        Assert.That((int)SendConfigurationStatus.Sent, Is.EqualTo(3));
    }

    [Test]
    public void BuildReport_Uniques_PerMailout_ClickCountsAsOpen()
    {
        Assert.That(sql, Does.Contain("COUNT(DISTINCT CASE WHEN H.[EmailStatisticsHitsType] IN (1, 2) THEN H.[EmailStatisticsHitsMailoutGUID] END) AS [UniqueOpens]"));
        Assert.That(sql, Does.Contain("COUNT(DISTINCT CASE WHEN H.[EmailStatisticsHitsType] = 2 THEN H.[EmailStatisticsHitsMailoutGUID] END) AS [UniqueClicks]"));
        Assert.That(sql, Does.Contain("COUNT(DISTINCT CASE WHEN H.[EmailStatisticsHitsType] = 5 THEN H.[EmailStatisticsHitsMailoutGUID] END) AS [Unsubscribes]"));
        Assert.That(sql, Does.Contain("SUM(CASE WHEN H.[EmailStatisticsHitsType] = 0 THEN 1 ELSE 0 END) AS [Sent]"));
    }

    [Test]
    public void BuildReport_BouncesAndSpam_FromStatisticsTableOnly()
    {
        // Delivery providers write them without hits.
        Assert.That(sql, Does.Not.Contain("[EmailStatisticsHitsType] = 3"));
        Assert.That(sql, Does.Not.Contain("[EmailStatisticsHitsType] = 4"));
        Assert.That(sql, Does.Contain("S.[EmailStatisticsEmailHardBounces]"));
        Assert.That(sql, Does.Contain("S.[EmailStatisticsSpamReports]"));
    }

    [Test]
    public void BuildReport_Periods_WeeksFromMonday_FirstPeriodAtRangeStart()
    {
        Assert.That(sql, Does.Contain("WHEN 1 THEN DATEADD(day, -(DATEDIFF(day, '19000101', D.[Day]) % 7), D.[Day])"));
        Assert.That(sql, Does.Contain("WHEN 2 THEN DATEFROMPARTS(YEAR(D.[Day]), MONTH(D.[Day]), 1)"));
        Assert.That(sql, Does.Contain("CASE WHEN P.[Start] < CAST(@From AS date) THEN CAST(@From AS date) ELSE P.[Start] END AS [PeriodStart]"));
        Assert.That(new DateOnly(1900, 1, 1).DayOfWeek, Is.EqualTo(DayOfWeek.Monday));
        Assert.That((int)StatsGrouping.Week, Is.EqualTo(1));
        Assert.That((int)StatsGrouping.Month, Is.EqualTo(2));
    }

    [Test]
    public void BuildReport_DisplayName_PrimaryLanguageFirst()
    {
        Assert.That(sql, Does.Contain("CASE WHEN LM.[ContentItemLanguageMetadataContentLanguageID] = C.[EmailChannelPrimaryContentLanguageID] THEN 0 ELSE 1 END"));
        Assert.That(sql, Does.Contain("COALESCE(M.[ContentLanguageName], P.[ContentLanguageName])"));
    }

    [Test]
    public void BuildReport_AutomatedEmails_OnlyWithStatistics()
    {
        int start = sql.IndexOf("E.[Purpose] AS [EmailConfigurationPurpose]", StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThan(0));
        Assert.That(sql[start..], Does.Contain("FROM @Emails E\nCROSS APPLY ("));
    }

    [Test]
    public void Purposes_AndHitTypes_MatchProductValues() => Assert.Multiple(() =>
                                                                  {
                                                                      Assert.That(EmailPurpose.Regular.ToString(), Is.EqualTo("Regular"));
                                                                      Assert.That((int)EmailStatisticsHitsType.Sent, Is.Zero);
                                                                      Assert.That((int)EmailStatisticsHitsType.Open, Is.EqualTo(1));
                                                                      Assert.That((int)EmailStatisticsHitsType.Click, Is.EqualTo(2));
                                                                      Assert.That((int)EmailStatisticsHitsType.Unsubscribe, Is.EqualTo(5));
                                                                  });

    /// <summary>
    /// The SQL reads these columns; the product's Info classes must still have them.
    /// </summary>
    [Test]
    public void Sql_UsesColumnsOfTheInfoClasses()
    {
        string[] columns =
        [
            nameof(EmailConfigurationInfo.EmailConfigurationID),
            nameof(EmailConfigurationInfo.EmailConfigurationName),
            nameof(EmailConfigurationInfo.EmailConfigurationPurpose),
            nameof(EmailConfigurationInfo.EmailConfigurationEmailChannelID),
            nameof(EmailConfigurationInfo.EmailConfigurationContentItemID),
            nameof(EmailChannelInfo.EmailChannelID),
            nameof(EmailChannelInfo.EmailChannelChannelID),
            nameof(EmailChannelInfo.EmailChannelPrimaryContentLanguageID),
            nameof(SendConfigurationInfo.SendConfigurationEmailConfigurationID),
            nameof(SendConfigurationInfo.SendConfigurationStatus),
            nameof(SendConfigurationInfo.SendConfigurationScheduledTime),
            nameof(SendConfigurationInfo.SendConfigurationRecipientListID),
            nameof(EmailStatisticsInfo.EmailStatisticsID),
            nameof(EmailStatisticsInfo.EmailStatisticsEmailConfigurationID),
            nameof(EmailStatisticsInfo.EmailStatisticsTotalSent),
            nameof(EmailStatisticsInfo.EmailStatisticsEmailsDelivered),
            nameof(EmailStatisticsInfo.EmailStatisticsEmailUniqueOpens),
            nameof(EmailStatisticsInfo.EmailStatisticsEmailUniqueClicks),
            nameof(EmailStatisticsInfo.EmailStatisticsEmailSoftBounces),
            nameof(EmailStatisticsInfo.EmailStatisticsEmailHardBounces),
            nameof(EmailStatisticsInfo.EmailStatisticsUniqueUnsubscribes),
            nameof(EmailStatisticsInfo.EmailStatisticsSpamReports),
            nameof(EmailStatisticsHitsInfo.EmailStatisticsHitsEmailConfigurationID),
            nameof(EmailStatisticsHitsInfo.EmailStatisticsHitsType),
            nameof(EmailStatisticsHitsInfo.EmailStatisticsHitsTime),
            nameof(EmailStatisticsHitsInfo.EmailStatisticsHitsMailoutGUID),
            nameof(ContentLanguageInfo.ContentLanguageID),
            nameof(ContentLanguageInfo.ContentLanguageName),
            nameof(ContactGroupInfo.ContactGroupID),
            nameof(ContactGroupInfo.ContactGroupDisplayName),
        ];

        Assert.That(columns, Is.All.Matches<string>(column => sql.Contains($"[{column}]", StringComparison.Ordinal)));
    }
}
