using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.RecipientLists;

/// <summary>
/// Builds the recipient lists batch. Only constant SQL fragments are combined; all values are parameters.
/// Tables and columns are those of <c>CMS.ContactManagement.ContactGroupInfo</c> (<c>OM_ContactGroup</c>),
/// <c>ContactGroupMemberInfo</c> (<c>OM_ContactGroupMember</c>), <c>ContactInfo</c> (<c>OM_Contact</c>),
/// <c>CMS.EmailMarketing.EmailSubscriptionConfirmationInfo</c> (<c>EmailLibrary_EmailSubscriptionConfirmation</c>)
/// and <c>EmailBounceInfo</c> (<c>EmailLibrary_EmailBounce</c>).
/// </summary>
/// <remarks>
/// <para>
/// A recipient list is a contact group with <c>ContactGroupIsRecipientList = 1</c>. Membership (<c>OM_ContactGroupMember</c>, contacts only:
/// <c>ContactGroupMemberType = 0</c>) is current state without dates; unsubscribing keeps the member row.
/// <c>EmailSubscriptionConfirmation</c> is an event history: confirming a subscription inserts an approved row, revoking it
/// (unsubscribe) inserts an unapproved row. The state of a contact + list on a day is its latest row on or before that day.
/// </para>
/// <para>
/// Current statuses follow the native recipient list overview (<c>RecipientListStatisticsService</c>), per member:
/// receiving = latest row approved and email not bounced; bounced = latest row approved and email bounced (an <c>EmailBounce</c> row of the
/// contact email that is a hard bounce or has at least <see cref="SoftBounceLimitParameter"/> soft bounces); unsubscribed = latest row not approved;
/// not confirmed = no row (double opt-in pending, or added without confirmation).
/// </para>
/// <para>
/// The batch starts with one row (<see cref="AvailableColumn"/>): <c>0</c> when a table does not exist, and then returns nothing else.
/// It aggregates the confirmation rows up to the end of the range per day and list into a table variable (state changes via <c>LAG</c>
/// per contact + list, counted only for current members, see <see cref="DailyQuery"/>), and then returns, in order:
/// </para>
/// <list type="number">
/// <item>All recipient lists by display name, with events, subscribers and current statuses (list filter does not apply).</item>
/// <item>Events and subscriber changes per day from the start of the previous period to the end of the range (list filter applied).</item>
/// <item>Totals, one row: subscribers before the range (list filter applied).</item>
/// </list>
/// A <see cref="ListIdParameter"/> that is not a recipient list is set to 0 (all lists). Rows of deleted contacts are deleted with them,
/// also for past dates. <c>EmailSubscriptionConfirmationDate</c> is compared as stored.
/// </remarks>
internal static class RecipientListsSql
{
    /// <summary>Start of the previous period (inclusive).</summary>
    public const string PreviousFromParameter = "@PreviousFrom";

    /// <summary>Start of the range (inclusive).</summary>
    public const string FromParameter = "@From";

    /// <summary>Day after the end of the range (exclusive).</summary>
    public const string ToExclusiveParameter = "@ToExclusive";

    /// <summary>Contact group ID of the list filter, 0 for all lists.</summary>
    public const string ListIdParameter = "@ListId";

    /// <summary>Soft bounces after which an email counts as bounced (<c>BouncedEmailsGlobalOptions.SoftBounceLimit</c>).</summary>
    public const string SoftBounceLimitParameter = "@SoftBounceLimit";

    public const string AvailableColumn = "RecipientListsAvailable";
    public const string ListIdColumn = "ContactGroupID";
    public const string ListNameColumn = "ContactGroupDisplayName";
    public const string SubscriptionsColumn = "Subscriptions";
    public const string UnsubscriptionsColumn = "Unsubscriptions";
    public const string SubscribersColumn = "Subscribers";
    public const string PreviousSubscribersColumn = "PreviousSubscribers";
    public const string ReceivingColumn = "Receiving";
    public const string BouncedColumn = "Bounced";
    public const string UnsubscribedColumn = "Unsubscribed";
    public const string NotConfirmedColumn = "NotConfirmed";
    public const string DateColumn = "EventDate";
    public const string SubscriberChangeColumn = "SubscriberChange";
    public const string SubscribersBeforeColumn = "SubscribersBefore";

    private static readonly string availabilityCheck = StatsSql.BuildAvailabilityCheck(
        AvailableColumn,
        "OM_ContactGroup",
        "OM_ContactGroupMember",
        "OM_Contact",
        "EmailLibrary_EmailSubscriptionConfirmation",
        "EmailLibrary_EmailBounce");

    /// <summary>
    /// Lists, their current members (distinct contacts) and confirmation rows up to the end of the range, per day and list. Per row:
    /// <c>Change</c> = state change of a current member + list (subscribed after the row minus subscribed before it: not subscribed to
    /// subscribed = +1, subscribed to unsubscribed = -1, subscribing again while subscribed or unsubscribing while not subscribed = 0; 0 for
    /// contacts that are no longer members);
    /// <c>ContactChange</c> = change of "subscribed to at least one list" of the contact (from its running number of subscribed lists).
    /// Ties on date are ordered by row ID.
    /// </summary>
    private const string DailyQuery = """
        DECLARE @Lists TABLE ([ListID] int PRIMARY KEY);
        INSERT INTO @Lists
        SELECT G.[ContactGroupID] FROM [OM_ContactGroup] G WHERE G.[ContactGroupIsRecipientList] = 1;

        IF NOT EXISTS (SELECT 1 FROM @Lists L WHERE L.[ListID] = @ListId)
            SET @ListId = 0;

        DECLARE @Members TABLE ([ListID] int NOT NULL, [ContactID] int NOT NULL, PRIMARY KEY ([ListID], [ContactID]));
        INSERT INTO @Members
        SELECT DISTINCT M.[ContactGroupMemberContactGroupID], M.[ContactGroupMemberRelatedID]
        FROM [OM_ContactGroupMember] M
        INNER JOIN @Lists L ON L.[ListID] = M.[ContactGroupMemberContactGroupID]
        WHERE M.[ContactGroupMemberType] = 0;

        DECLARE @Daily TABLE (
            [Day] date NOT NULL,
            [ListID] int NOT NULL,
            [Subscriptions] int NOT NULL,
            [Unsubscriptions] int NOT NULL,
            [Change] int NOT NULL,
            [ContactChange] int NOT NULL,
            PRIMARY KEY ([Day], [ListID]));

        WITH [Events] AS (
            SELECT
                E.[EmailSubscriptionConfirmationID],
                E.[EmailSubscriptionConfirmationContactID],
                E.[EmailSubscriptionConfirmationRecipientListID],
                E.[EmailSubscriptionConfirmationDate],
                E.[EmailSubscriptionConfirmationIsApproved],
                CASE WHEN M.[ContactID] IS NULL THEN 0 ELSE
                    CASE WHEN E.[EmailSubscriptionConfirmationIsApproved] = 1 THEN 1 ELSE 0 END
                    - CASE WHEN LAG(E.[EmailSubscriptionConfirmationIsApproved]) OVER (
                        PARTITION BY E.[EmailSubscriptionConfirmationContactID], E.[EmailSubscriptionConfirmationRecipientListID]
                        ORDER BY E.[EmailSubscriptionConfirmationDate], E.[EmailSubscriptionConfirmationID]) = 1 THEN 1 ELSE 0 END
                END AS [Change]
            FROM [EmailLibrary_EmailSubscriptionConfirmation] E
            INNER JOIN @Lists L ON L.[ListID] = E.[EmailSubscriptionConfirmationRecipientListID]
            LEFT JOIN @Members M ON M.[ListID] = E.[EmailSubscriptionConfirmationRecipientListID]
                AND M.[ContactID] = E.[EmailSubscriptionConfirmationContactID]
            WHERE E.[EmailSubscriptionConfirmationDate] < @ToExclusive
        ), [Levels] AS (
            SELECT
                E.*,
                SUM(E.[Change]) OVER (
                    PARTITION BY E.[EmailSubscriptionConfirmationContactID]
                    ORDER BY E.[EmailSubscriptionConfirmationDate], E.[EmailSubscriptionConfirmationID]
                    ROWS UNBOUNDED PRECEDING) AS [SubscribedLists]
            FROM [Events] E
        )
        INSERT INTO @Daily
        SELECT
            CAST(L.[EmailSubscriptionConfirmationDate] AS date),
            L.[EmailSubscriptionConfirmationRecipientListID],
            SUM(CASE WHEN L.[EmailSubscriptionConfirmationIsApproved] = 1 THEN 1 ELSE 0 END),
            SUM(CASE WHEN L.[EmailSubscriptionConfirmationIsApproved] = 0 THEN 1 ELSE 0 END),
            SUM(L.[Change]),
            SUM(CASE WHEN L.[SubscribedLists] > 0 THEN 1 ELSE 0 END - CASE WHEN L.[SubscribedLists] - L.[Change] > 0 THEN 1 ELSE 0 END)
        FROM [Levels] L
        GROUP BY CAST(L.[EmailSubscriptionConfirmationDate] AS date), L.[EmailSubscriptionConfirmationRecipientListID];
        """;

    /// <summary>
    /// Current status of each member + list (all rows, not limited to the range), as the native overview counts them.
    /// </summary>
    private const string StatusQuery = """
        DECLARE @Status TABLE (
            [ListID] int PRIMARY KEY,
            [Receiving] int NOT NULL,
            [Bounced] int NOT NULL,
            [Unsubscribed] int NOT NULL,
            [NotConfirmed] int NOT NULL);

        WITH [Latest] AS (
            SELECT
                E.[EmailSubscriptionConfirmationContactID],
                E.[EmailSubscriptionConfirmationRecipientListID],
                E.[EmailSubscriptionConfirmationIsApproved],
                ROW_NUMBER() OVER (
                    PARTITION BY E.[EmailSubscriptionConfirmationContactID], E.[EmailSubscriptionConfirmationRecipientListID]
                    ORDER BY E.[EmailSubscriptionConfirmationDate] DESC, E.[EmailSubscriptionConfirmationID] DESC) AS [RowNumber]
            FROM [EmailLibrary_EmailSubscriptionConfirmation] E
            INNER JOIN @Lists L ON L.[ListID] = E.[EmailSubscriptionConfirmationRecipientListID]
        ), [MemberStatus] AS (
            SELECT
                M.[ListID],
                LT.[EmailSubscriptionConfirmationIsApproved] AS [Approved],
                CASE WHEN EXISTS (
                    SELECT 1
                    FROM [OM_Contact] C
                    INNER JOIN [EmailLibrary_EmailBounce] B ON B.[EmailBounceEmailAddress] = C.[ContactEmail]
                    WHERE C.[ContactID] = M.[ContactID]
                        AND (B.[EmailBounceIsHardBounce] = 1 OR B.[EmailBounceSoftBounceCount] >= @SoftBounceLimit))
                    THEN 1 ELSE 0 END AS [IsBounced]
            FROM @Members M
            LEFT JOIN [Latest] LT ON LT.[EmailSubscriptionConfirmationContactID] = M.[ContactID]
                AND LT.[EmailSubscriptionConfirmationRecipientListID] = M.[ListID]
                AND LT.[RowNumber] = 1
        )
        INSERT INTO @Status
        SELECT
            S.[ListID],
            SUM(CASE WHEN S.[Approved] = 1 AND S.[IsBounced] = 0 THEN 1 ELSE 0 END),
            SUM(CASE WHEN S.[Approved] = 1 AND S.[IsBounced] = 1 THEN 1 ELSE 0 END),
            SUM(CASE WHEN S.[Approved] = 0 THEN 1 ELSE 0 END),
            SUM(CASE WHEN S.[Approved] IS NULL THEN 1 ELSE 0 END)
        FROM [MemberStatus] S
        GROUP BY S.[ListID];
        """;

    // 1. All recipient lists (also without members), list filter not applied.
    private const string ListsQuery = """
        SELECT
            G.[ContactGroupID],
            G.[ContactGroupDisplayName],
            ISNULL(SUM(CASE WHEN D.[Day] >= @From THEN D.[Subscriptions] END), 0) AS [Subscriptions],
            ISNULL(SUM(CASE WHEN D.[Day] >= @From THEN D.[Unsubscriptions] END), 0) AS [Unsubscriptions],
            ISNULL(SUM(D.[Change]), 0) AS [Subscribers],
            ISNULL(SUM(CASE WHEN D.[Day] < @From THEN D.[Change] END), 0) AS [PreviousSubscribers],
            ISNULL(MAX(S.[Receiving]), 0) AS [Receiving],
            ISNULL(MAX(S.[Bounced]), 0) AS [Bounced],
            ISNULL(MAX(S.[Unsubscribed]), 0) AS [Unsubscribed],
            ISNULL(MAX(S.[NotConfirmed]), 0) AS [NotConfirmed]
        FROM [OM_ContactGroup] G
        INNER JOIN @Lists L ON L.[ListID] = G.[ContactGroupID]
        LEFT JOIN @Daily D ON D.[ListID] = G.[ContactGroupID]
        LEFT JOIN @Status S ON S.[ListID] = G.[ContactGroupID]
        GROUP BY G.[ContactGroupID], G.[ContactGroupDisplayName]
        ORDER BY G.[ContactGroupDisplayName], G.[ContactGroupID];
        """;

    // 2. Events per day, previous period + range. One list: its state changes; all lists: distinct contact changes.
    private const string EventsQuery = """
        SELECT
            D.[Day] AS [EventDate],
            SUM(D.[Subscriptions]) AS [Subscriptions],
            SUM(D.[Unsubscriptions]) AS [Unsubscriptions],
            SUM(CASE WHEN @ListId = 0 THEN D.[ContactChange] ELSE D.[Change] END) AS [SubscriberChange]
        FROM @Daily D
        WHERE D.[Day] >= @PreviousFrom
            AND (@ListId = 0 OR D.[ListID] = @ListId)
        GROUP BY D.[Day];
        """;

    // 3. Totals, one row.
    private const string TotalsQuery = """
        SELECT
            ISNULL(SUM(CASE WHEN D.[Day] < @From AND (@ListId = 0 OR D.[ListID] = @ListId)
                THEN CASE WHEN @ListId = 0 THEN D.[ContactChange] ELSE D.[Change] END END), 0) AS [SubscribersBefore]
        FROM @Daily D;
        """;

    /// <summary>
    /// Builds the batch (see the class remarks).
    /// </summary>
    public static string BuildReport() =>
        string.Join(Environment.NewLine, "SET NOCOUNT ON;", availabilityCheck, DailyQuery, StatusQuery, ListsQuery, EventsQuery, TotalsQuery);
}
