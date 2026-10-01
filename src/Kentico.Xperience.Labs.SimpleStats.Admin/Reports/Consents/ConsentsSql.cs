using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Consents;

/// <summary>
/// Builds the consents batch. Only constant SQL fragments are combined; all values are parameters.
/// Tables and columns are those of <c>CMS.DataProtection.ConsentInfo</c> (<c>CMS_Consent</c>)
/// and <c>ConsentAgreementInfo</c> (<c>CMS_ConsentAgreement</c>), and <c>CMS.ContentEngine.ContentLanguageInfo</c> (<c>CMS_ContentLanguage</c>, for the default language of admin links).
/// </summary>
/// <remarks>
/// <para>
/// <c>CMS_ConsentAgreement</c> is an event history (<c>ConsentAgreementService</c>): agreeing inserts a row (not revoked, hash of the
/// consent text), revoking inserts a row (revoked, no hash). The state of a contact + consent on a day is its latest row on or before that day.
/// </para>
/// <para>
/// The batch starts with one row (<see cref="AvailableColumn"/>): <c>0</c> when a table does not exist, and then returns nothing else.
/// It first reads all agreement rows up to the end of the range once and aggregates them per day and consent into a table variable
/// (state changes via <c>LAG</c> per contact + consent, see <see cref="DailyQuery"/>), and then returns, in order:
/// </para>
/// <list type="number">
/// <item>All consents by display name, with agreements, revocations, agreed contacts and older-text contacts (consent filter does not apply).</item>
/// <item>Events and agreed contacts changes per day from the start of the previous period to the end of the range (consent filter applied).</item>
/// <item>Totals, one row: agreed contacts before the range (consent filter applied) and distinct agreed contacts on the range end (all consents) and the default content language.</item>
/// </list>
/// A <see cref="ConsentIdParameter"/> that is not a consent is set to 0 (all consents). Agreements of deleted contacts are deleted with them,
/// also for past dates. <c>ConsentAgreementTime</c> is compared as stored.
/// </remarks>
internal static class ConsentsSql
{
    /// <summary>Start of the previous period (inclusive).</summary>
    public const string PreviousFromParameter = "@PreviousFrom";

    /// <summary>Start of the range (inclusive).</summary>
    public const string FromParameter = "@From";

    /// <summary>Day after the end of the range (exclusive).</summary>
    public const string ToExclusiveParameter = "@ToExclusive";

    /// <summary>Consent ID of the consent filter, 0 for all consents.</summary>
    public const string ConsentIdParameter = "@ConsentId";

    public const string AvailableColumn = "ConsentsAvailable";
    public const string ConsentIdColumn = "ConsentID";
    public const string ConsentNameColumn = "ConsentDisplayName";
    public const string AgreementsColumn = "Agreements";
    public const string PreviousAgreementsColumn = "PreviousAgreements";
    public const string RevocationsColumn = "Revocations";
    public const string AgreedColumn = "AgreedContacts";
    public const string PreviousAgreedColumn = "PreviousAgreedContacts";
    public const string OlderTextColumn = "OlderText";
    public const string DateColumn = "EventDate";
    public const string AgreedChangeColumn = "AgreedChange";
    public const string AgreedBeforeColumn = "AgreedBefore";
    public const string AllAgreedColumn = "AllAgreedContacts";
    public const string DefaultLanguageColumn = "DefaultLanguage";

    private static readonly string availabilityCheck = StatsSql.BuildAvailabilityCheck(
        AvailableColumn,
        "CMS_Consent",
        "CMS_ConsentAgreement");

    /// <summary>
    /// Agreement rows up to the end of the range, per day and consent. Per row:
    /// <c>Change</c> = state change of the contact + consent (agreed after the row minus agreed before it: not agreed to agreed = +1,
    /// agreed to revoked = -1, agreeing again while agreed or revoking while not agreed = 0);
    /// <c>ContactChange</c> = change of "agrees to at least one consent" of the contact (from its running number of agreed consents);
    /// <c>OlderText</c> = 1 on the latest row of an agreed contact + consent whose hash is not the consent's current hash.
    /// Ties on time are ordered by row ID.
    /// </summary>
    private const string DailyQuery = """
        IF NOT EXISTS (SELECT 1 FROM [CMS_Consent] C WHERE C.[ConsentID] = @ConsentId)
            SET @ConsentId = 0;

        DECLARE @Daily TABLE (
            [Day] date NOT NULL,
            [ConsentID] int NOT NULL,
            [Agreements] int NOT NULL,
            [Revocations] int NOT NULL,
            [Change] int NOT NULL,
            [ContactChange] int NOT NULL,
            [OlderText] int NOT NULL,
            PRIMARY KEY ([Day], [ConsentID]));

        WITH [Events] AS (
            SELECT
                A.[ConsentAgreementID],
                A.[ConsentAgreementContactID],
                A.[ConsentAgreementConsentID],
                A.[ConsentAgreementTime],
                A.[ConsentAgreementRevoked],
                CASE WHEN A.[ConsentAgreementRevoked] = 0 THEN 1 ELSE 0 END
                    - CASE WHEN LAG(A.[ConsentAgreementRevoked]) OVER (
                        PARTITION BY A.[ConsentAgreementContactID], A.[ConsentAgreementConsentID]
                        ORDER BY A.[ConsentAgreementTime], A.[ConsentAgreementID]) = 0 THEN 1 ELSE 0 END AS [Change],
                CASE WHEN A.[ConsentAgreementRevoked] = 0
                    AND ISNULL(A.[ConsentAgreementConsentHash], N'') <> ISNULL(C.[ConsentHash], N'')
                    AND ROW_NUMBER() OVER (
                        PARTITION BY A.[ConsentAgreementContactID], A.[ConsentAgreementConsentID]
                        ORDER BY A.[ConsentAgreementTime] DESC, A.[ConsentAgreementID] DESC) = 1
                    THEN 1 ELSE 0 END AS [OlderText]
            FROM [CMS_ConsentAgreement] A
            INNER JOIN [CMS_Consent] C ON C.[ConsentID] = A.[ConsentAgreementConsentID]
            WHERE A.[ConsentAgreementTime] < @ToExclusive
        ), [Levels] AS (
            SELECT
                E.*,
                SUM(E.[Change]) OVER (
                    PARTITION BY E.[ConsentAgreementContactID]
                    ORDER BY E.[ConsentAgreementTime], E.[ConsentAgreementID]
                    ROWS UNBOUNDED PRECEDING) AS [AgreedConsents]
            FROM [Events] E
        )
        INSERT INTO @Daily
        SELECT
            CAST(L.[ConsentAgreementTime] AS date),
            L.[ConsentAgreementConsentID],
            SUM(CASE WHEN L.[ConsentAgreementRevoked] = 0 THEN 1 ELSE 0 END),
            SUM(CASE WHEN L.[ConsentAgreementRevoked] = 1 THEN 1 ELSE 0 END),
            SUM(L.[Change]),
            SUM(CASE WHEN L.[AgreedConsents] > 0 THEN 1 ELSE 0 END - CASE WHEN L.[AgreedConsents] - L.[Change] > 0 THEN 1 ELSE 0 END),
            SUM(L.[OlderText])
        FROM [Levels] L
        GROUP BY CAST(L.[ConsentAgreementTime] AS date), L.[ConsentAgreementConsentID];
        """;

    // 1. All consents (also without agreements), consent filter not applied.
    private const string ConsentsQuery = """
        SELECT
            C.[ConsentID],
            C.[ConsentDisplayName],
            ISNULL(SUM(CASE WHEN D.[Day] >= @From THEN D.[Agreements] END), 0) AS [Agreements],
            ISNULL(SUM(CASE WHEN D.[Day] >= @PreviousFrom AND D.[Day] < @From THEN D.[Agreements] END), 0) AS [PreviousAgreements],
            ISNULL(SUM(CASE WHEN D.[Day] >= @From THEN D.[Revocations] END), 0) AS [Revocations],
            ISNULL(SUM(D.[Change]), 0) AS [AgreedContacts],
            ISNULL(SUM(CASE WHEN D.[Day] < @From THEN D.[Change] END), 0) AS [PreviousAgreedContacts],
            ISNULL(SUM(D.[OlderText]), 0) AS [OlderText]
        FROM [CMS_Consent] C
        LEFT JOIN @Daily D ON D.[ConsentID] = C.[ConsentID]
        GROUP BY C.[ConsentID], C.[ConsentDisplayName]
        ORDER BY C.[ConsentDisplayName], C.[ConsentID];
        """;

    // 2. Events per day, previous period + range. One consent: its state changes; all consents: distinct contact changes.
    private const string EventsQuery = """
        SELECT
            D.[Day] AS [EventDate],
            SUM(D.[Agreements]) AS [Agreements],
            SUM(D.[Revocations]) AS [Revocations],
            SUM(CASE WHEN @ConsentId = 0 THEN D.[ContactChange] ELSE D.[Change] END) AS [AgreedChange]
        FROM @Daily D
        WHERE D.[Day] >= @PreviousFrom
            AND (@ConsentId = 0 OR D.[ConsentID] = @ConsentId)
        GROUP BY D.[Day];
        """;

    // 3. Totals, one row.
    private const string TotalsQuery = """
        SELECT
            ISNULL(SUM(CASE WHEN D.[Day] < @From AND (@ConsentId = 0 OR D.[ConsentID] = @ConsentId)
                THEN CASE WHEN @ConsentId = 0 THEN D.[ContactChange] ELSE D.[Change] END END), 0) AS [AgreedBefore],
            ISNULL(SUM(D.[ContactChange]), 0) AS [AllAgreedContacts],
            (SELECT TOP (1) L.[ContentLanguageName] FROM [CMS_ContentLanguage] L WHERE L.[ContentLanguageIsDefault] = 1 ORDER BY L.[ContentLanguageID]) AS [DefaultLanguage]
        FROM @Daily D;
        """;

    /// <summary>
    /// Builds the batch (see the class remarks).
    /// </summary>
    public static string BuildReport() =>
        string.Join(Environment.NewLine, "SET NOCOUNT ON;", availabilityCheck, DailyQuery, ConsentsQuery, EventsQuery, TotalsQuery);
}
