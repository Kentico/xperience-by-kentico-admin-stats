using CMS.ContentEngine;
using CMS.DataProtection;

using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Consents;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

/// <summary>
/// The batch was also run against a local database with agree / revoke / agree, agree twice and revoke-only histories
/// (see the report notes); these tests pin the SQL shape that makes those cases right.
/// </summary>
public class ConsentsSqlTests
{
    private static readonly string sql = ConsentsSql.BuildReport().ReplaceLineEndings("\n");

    [Test]
    public void BuildReport_ChecksTablesFirst_AndReturnsEarly()
    {
        string[] tables = ["CMS_Consent", "CMS_ConsentAgreement"];
        Assert.That(tables, Is.All.Matches<string>(table => sql.Contains($"OBJECT_ID(N'[{table}]', N'U') IS NULL", StringComparison.Ordinal)));
        Assert.That(sql, Does.Contain($"SELECT CAST(0 AS bit) AS [{ConsentsSql.AvailableColumn}];"));
        Assert.That(sql.IndexOf("RETURN;", StringComparison.Ordinal), Is.LessThan(sql.IndexOf("FROM [CMS_ConsentAgreement] A", StringComparison.Ordinal)));
    }

    [Test]
    public void BuildReport_UsesParameters()
    {
        Assert.That(sql, Does.Contain("A.[ConsentAgreementTime] < @ToExclusive"));
        Assert.That(sql, Does.Contain("D.[Day] >= @PreviousFrom"));
        Assert.That(sql, Does.Contain("D.[Day] < @From"));
        Assert.That(sql, Does.Contain("(@ConsentId = 0 OR D.[ConsentID] = @ConsentId)"));
    }

    [Test]
    public void BuildReport_UnknownConsent_MeansAll()
    {
        Assert.That(sql, Does.Contain("IF NOT EXISTS (SELECT 1 FROM [CMS_Consent] C WHERE C.[ConsentID] = @ConsentId)"));
        Assert.That(sql, Does.Contain("SET @ConsentId = 0;"));
        Assert.That(sql.IndexOf("SET @ConsentId = 0;", StringComparison.Ordinal), Is.LessThan(sql.IndexOf("FROM @Daily D", StringComparison.Ordinal)));
    }

    [Test]
    public void BuildReport_StateChanges_FromPreviousRowOfContactAndConsent()
    {
        // Agreed after the row minus agreed before it: agree twice = 0, revoke without agree = 0, agree -> revoke -> agree = +1 -1 +1.
        Assert.That(sql, Does.Contain("CASE WHEN A.[ConsentAgreementRevoked] = 0 THEN 1 ELSE 0 END"));
        Assert.That(sql, Does.Contain("- CASE WHEN LAG(A.[ConsentAgreementRevoked]) OVER ("));
        Assert.That(sql, Does.Contain("PARTITION BY A.[ConsentAgreementContactID], A.[ConsentAgreementConsentID]"));
        Assert.That(sql, Does.Contain("ORDER BY A.[ConsentAgreementTime], A.[ConsentAgreementID]) = 0 THEN 1 ELSE 0 END AS [Change]"));
    }

    [Test]
    public void BuildReport_AllConsents_CountsDistinctContacts()
    {
        Assert.That(sql, Does.Contain("PARTITION BY E.[ConsentAgreementContactID]"));
        Assert.That(sql, Does.Contain("CASE WHEN L.[AgreedConsents] > 0 THEN 1 ELSE 0 END - CASE WHEN L.[AgreedConsents] - L.[Change] > 0 THEN 1 ELSE 0 END"));
        Assert.That(sql, Does.Contain("CASE WHEN @ConsentId = 0 THEN D.[ContactChange] ELSE D.[Change] END"));
    }

    [Test]
    public void BuildReport_OlderText_ComparesLatestAgreementHashWithCurrentHash()
    {
        Assert.That(sql, Does.Contain("ISNULL(A.[ConsentAgreementConsentHash], N'') <> ISNULL(C.[ConsentHash], N'')"));
        Assert.That(sql, Does.Contain("ORDER BY A.[ConsentAgreementTime] DESC, A.[ConsentAgreementID] DESC) = 1"));
    }

    [Test]
    public void BuildReport_ConsentsList_IgnoresFilter_KeepsConsentsWithoutAgreements()
    {
        int start = sql.IndexOf("FROM [CMS_Consent] C\nLEFT JOIN @Daily D", StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThan(0));

        string consents = sql[start..sql.IndexOf("ORDER BY C.[ConsentDisplayName]", start, StringComparison.Ordinal)];
        Assert.That(consents, Does.Not.Contain("@ConsentId"));
    }

    /// <summary>
    /// The SQL reads these columns; the product's Info classes must still have them.
    /// </summary>
    [Test]
    public void Sql_UsesColumnsOfTheInfoClasses()
    {
        string[] columns =
        [
            nameof(ConsentInfo.ConsentID),
            nameof(ConsentInfo.ConsentDisplayName),
            nameof(ConsentInfo.ConsentHash),
            nameof(ConsentAgreementInfo.ConsentAgreementID),
            nameof(ConsentAgreementInfo.ConsentAgreementContactID),
            nameof(ConsentAgreementInfo.ConsentAgreementConsentID),
            nameof(ConsentAgreementInfo.ConsentAgreementRevoked),
            nameof(ConsentAgreementInfo.ConsentAgreementConsentHash),
            nameof(ConsentAgreementInfo.ConsentAgreementTime),
            nameof(ContentLanguageInfo.ContentLanguageName),
            nameof(ContentLanguageInfo.ContentLanguageIsDefault),
        ];

        Assert.That(columns, Is.All.Matches<string>(column => sql.Contains($"[{column}]", StringComparison.Ordinal)));
    }
}
