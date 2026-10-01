using CMS.ContactManagement;
using CMS.EmailMarketing;

using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.RecipientLists;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

/// <summary>
/// The batch was also run against a local database with subscribe / unsubscribe / subscribe, subscribe twice, unsubscribe-only,
/// member without rows, non-member with an approved row and duplicate member rows (see the report notes);
/// these tests pin the SQL shape that makes those cases right.
/// </summary>
public class RecipientListsSqlTests
{
    private static readonly string sql = RecipientListsSql.BuildReport().ReplaceLineEndings("\n");

    [Test]
    public void BuildReport_ChecksTablesFirst_AndReturnsEarly()
    {
        string[] tables = ["OM_ContactGroup", "OM_ContactGroupMember", "OM_Contact", "EmailLibrary_EmailSubscriptionConfirmation", "EmailLibrary_EmailBounce"];
        Assert.That(tables, Is.All.Matches<string>(table => sql.Contains($"OBJECT_ID(N'[{table}]', N'U') IS NULL", StringComparison.Ordinal)));
        Assert.That(sql, Does.Contain($"SELECT CAST(0 AS bit) AS [{RecipientListsSql.AvailableColumn}];"));
        Assert.That(
            sql.IndexOf("RETURN;", StringComparison.Ordinal),
            Is.LessThan(sql.IndexOf("FROM [EmailLibrary_EmailSubscriptionConfirmation] E", StringComparison.Ordinal)));
    }

    [Test]
    public void BuildReport_UsesParameters()
    {
        Assert.That(sql, Does.Contain("E.[EmailSubscriptionConfirmationDate] < @ToExclusive"));
        Assert.That(sql, Does.Contain("D.[Day] >= @PreviousFrom"));
        Assert.That(sql, Does.Contain("D.[Day] < @From"));
        Assert.That(sql, Does.Contain("(@ListId = 0 OR D.[ListID] = @ListId)"));
        Assert.That(sql, Does.Contain("B.[EmailBounceSoftBounceCount] >= @SoftBounceLimit"));
    }

    [Test]
    public void BuildReport_OnlyRecipientLists_UnknownListMeansAll()
    {
        Assert.That(sql, Does.Contain("WHERE G.[ContactGroupIsRecipientList] = 1;"));
        Assert.That(sql, Does.Contain("IF NOT EXISTS (SELECT 1 FROM @Lists L WHERE L.[ListID] = @ListId)"));
        Assert.That(sql.IndexOf("SET @ListId = 0;", StringComparison.Ordinal), Is.LessThan(sql.IndexOf("FROM @Daily D", StringComparison.Ordinal)));
        Assert.That(sql, Does.Contain("INNER JOIN @Lists L ON L.[ListID] = E.[EmailSubscriptionConfirmationRecipientListID]"));
    }

    [Test]
    public void BuildReport_Members_AreDistinctContacts()
    {
        Assert.That(sql, Does.Contain("SELECT DISTINCT M.[ContactGroupMemberContactGroupID], M.[ContactGroupMemberRelatedID]"));
        Assert.That(sql, Does.Contain("WHERE M.[ContactGroupMemberType] = 0;"));
    }

    [Test]
    public void BuildReport_StateChanges_FromPreviousRowOfContactAndList_OnlyForCurrentMembers()
    {
        // Subscribed after the row minus subscribed before it: subscribe twice = 0, unsubscribe without subscribe = 0,
        // subscribe -> unsubscribe -> subscribe = +1 -1 +1; non-members = 0.
        Assert.That(sql, Does.Contain("CASE WHEN M.[ContactID] IS NULL THEN 0 ELSE"));
        Assert.That(sql, Does.Contain("CASE WHEN E.[EmailSubscriptionConfirmationIsApproved] = 1 THEN 1 ELSE 0 END"));
        Assert.That(sql, Does.Contain("- CASE WHEN LAG(E.[EmailSubscriptionConfirmationIsApproved]) OVER ("));
        Assert.That(sql, Does.Contain("PARTITION BY E.[EmailSubscriptionConfirmationContactID], E.[EmailSubscriptionConfirmationRecipientListID]"));
        Assert.That(sql, Does.Contain("ORDER BY E.[EmailSubscriptionConfirmationDate], E.[EmailSubscriptionConfirmationID]) = 1 THEN 1 ELSE 0 END"));
    }

    [Test]
    public void BuildReport_AllLists_CountsDistinctContacts()
    {
        Assert.That(sql, Does.Contain("PARTITION BY E.[EmailSubscriptionConfirmationContactID]\n"));
        Assert.That(sql, Does.Contain("CASE WHEN L.[SubscribedLists] > 0 THEN 1 ELSE 0 END - CASE WHEN L.[SubscribedLists] - L.[Change] > 0 THEN 1 ELSE 0 END"));
        Assert.That(sql, Does.Contain("CASE WHEN @ListId = 0 THEN D.[ContactChange] ELSE D.[Change] END"));
    }

    [Test]
    public void BuildReport_Statuses_FollowNativeOverview()
    {
        // Latest row of all rows (not limited to the range) per member + list.
        Assert.That(sql, Does.Contain("ORDER BY E.[EmailSubscriptionConfirmationDate] DESC, E.[EmailSubscriptionConfirmationID] DESC) AS [RowNumber]"));
        Assert.That(sql, Does.Contain("INNER JOIN [EmailLibrary_EmailBounce] B ON B.[EmailBounceEmailAddress] = C.[ContactEmail]"));
        Assert.That(sql, Does.Contain("(B.[EmailBounceIsHardBounce] = 1 OR B.[EmailBounceSoftBounceCount] >= @SoftBounceLimit)"));
        Assert.That(sql, Does.Contain("SUM(CASE WHEN S.[Approved] = 1 AND S.[IsBounced] = 0 THEN 1 ELSE 0 END)"));
        Assert.That(sql, Does.Contain("SUM(CASE WHEN S.[Approved] = 1 AND S.[IsBounced] = 1 THEN 1 ELSE 0 END)"));
        Assert.That(sql, Does.Contain("SUM(CASE WHEN S.[Approved] = 0 THEN 1 ELSE 0 END)"));
        Assert.That(sql, Does.Contain("SUM(CASE WHEN S.[Approved] IS NULL THEN 1 ELSE 0 END)"));

        int status = sql.IndexOf("DECLARE @Status TABLE", StringComparison.Ordinal);
        string statusQuery = sql[status..sql.IndexOf("GROUP BY S.[ListID];", status, StringComparison.Ordinal)];
        Assert.That(statusQuery, Does.Not.Contain("@ToExclusive"));
    }

    [Test]
    public void BuildReport_ListsQuery_IgnoresFilter_KeepsListsWithoutRows()
    {
        int start = sql.IndexOf("FROM [OM_ContactGroup] G\nINNER JOIN @Lists L", StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThan(0));

        string lists = sql[start..sql.IndexOf("ORDER BY G.[ContactGroupDisplayName]", start, StringComparison.Ordinal)];
        Assert.That(lists, Does.Not.Contain("@ListId"));
        Assert.That(lists, Does.Contain("LEFT JOIN @Daily D"));
        Assert.That(lists, Does.Contain("LEFT JOIN @Status S"));
    }

    /// <summary>
    /// The SQL reads these columns; the product's Info classes must still have them.
    /// </summary>
    [Test]
    public void Sql_UsesColumnsOfTheInfoClasses()
    {
        string[] columns =
        [
            nameof(ContactGroupInfo.ContactGroupID),
            nameof(ContactGroupInfo.ContactGroupDisplayName),
            nameof(ContactGroupInfo.ContactGroupIsRecipientList),
            nameof(ContactGroupMemberInfo.ContactGroupMemberContactGroupID),
            nameof(ContactGroupMemberInfo.ContactGroupMemberType),
            nameof(ContactGroupMemberInfo.ContactGroupMemberRelatedID),
            nameof(ContactInfo.ContactID),
            nameof(ContactInfo.ContactEmail),
            nameof(EmailSubscriptionConfirmationInfo.EmailSubscriptionConfirmationID),
            nameof(EmailSubscriptionConfirmationInfo.EmailSubscriptionConfirmationContactID),
            nameof(EmailSubscriptionConfirmationInfo.EmailSubscriptionConfirmationRecipientListID),
            nameof(EmailSubscriptionConfirmationInfo.EmailSubscriptionConfirmationIsApproved),
            nameof(EmailSubscriptionConfirmationInfo.EmailSubscriptionConfirmationDate),
            nameof(EmailBounceInfo.EmailBounceEmailAddress),
            nameof(EmailBounceInfo.EmailBounceIsHardBounce),
            nameof(EmailBounceInfo.EmailBounceSoftBounceCount),
        ];

        Assert.That(columns, Is.All.Matches<string>(column => sql.Contains($"[{column}]", StringComparison.Ordinal)));
    }

    [Test]
    public void Sql_MemberTypeZero_IsTheProductContactMemberType() =>
        Assert.That((int)ContactGroupMemberTypeEnum.Contact, Is.Zero);
}
