using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Members;

/// <summary>
/// Builds the members batch. Only constant SQL fragments are combined; all values are parameters.
/// Tables and columns are those of <c>CMS.Membership.MemberInfo</c> (<c>CMS_Member</c>), <c>MemberRoleInfo</c> (<c>CMS_MemberRole</c>)
/// and <c>MemberRoleMemberInfo</c> (<c>CMS_MemberRoleMember</c>).
/// </summary>
/// <remarks>
/// The batch starts with one row (<see cref="AvailableColumn"/>): <c>0</c> when a table does not exist, and then returns nothing else.
/// Then, in order:
/// <list type="number">
/// <item>Members created per day from the start of the previous period to the end of the range, internal and external.</item>
/// <item>Totals, one row: members created before the range, all members, disabled members, members without a role (and of them new in the range).</item>
/// <item>Top member roles by current members, with the members created in the range and the number of roles.</item>
/// </list>
/// Members are counted as they are now: deleted members are gone, also for past periods. <c>MemberCreated</c> is compared as stored.
/// </remarks>
internal static class MembersSql
{
    /// <summary>Start of the previous period (inclusive).</summary>
    public const string PreviousFromParameter = "@PreviousFrom";

    /// <summary>Start of the range (inclusive).</summary>
    public const string FromParameter = "@From";

    /// <summary>Day after the end of the range (exclusive).</summary>
    public const string ToExclusiveParameter = "@ToExclusive";

    /// <summary>Maximum number of member roles.</summary>
    public const string LimitParameter = "@Limit";

    public const string AvailableColumn = "MembersAvailable";
    public const string DateColumn = "CreatedDate";
    public const string InternalColumn = "InternalCount";
    public const string ExternalColumn = "ExternalCount";
    public const string MembersBeforeColumn = "MembersBefore";
    public const string AllMembersColumn = "AllMembers";
    public const string DisabledColumn = "DisabledMembers";
    public const string NoRoleColumn = "NoRoleMembers";
    public const string NoRoleNewColumn = "NoRoleNewMembers";
    public const string RoleIdColumn = "MemberRoleID";
    public const string RoleNameColumn = "MemberRoleDisplayName";
    public const string MembersColumn = "MemberCount";
    public const string NewMembersColumn = "NewMemberCount";
    public const string RoleCountColumn = "RoleCount";

    private static readonly string availabilityCheck = StatsSql.BuildAvailabilityCheck(
        AvailableColumn,
        "CMS_Member",
        "CMS_MemberRole",
        "CMS_MemberRoleMember");

    // 1. Members created per day, previous period + range. External = signed up through an external sign-in provider.
    private const string DailyQuery = """
        SELECT
            CAST(M.[MemberCreated] AS date) AS [CreatedDate],
            SUM(CASE WHEN M.[MemberIsExternal] = 1 THEN 0 ELSE 1 END) AS [InternalCount],
            SUM(CASE WHEN M.[MemberIsExternal] = 1 THEN 1 ELSE 0 END) AS [ExternalCount]
        FROM [CMS_Member] M
        WHERE M.[MemberCreated] >= @PreviousFrom
            AND M.[MemberCreated] < @ToExclusive
        GROUP BY CAST(M.[MemberCreated] AS date);
        """;

    // 2. Totals, one row. Disabled and "no role" are current state.
    private const string TotalsQuery = """
        SELECT
            (SELECT COUNT(*) FROM [CMS_Member] M WHERE M.[MemberCreated] < @From) AS [MembersBefore],
            (SELECT COUNT(*) FROM [CMS_Member] M) AS [AllMembers],
            (SELECT COUNT(*) FROM [CMS_Member] M WHERE M.[MemberEnabled] = 0) AS [DisabledMembers],
            ISNULL(N.[NoRoleMembers], 0) AS [NoRoleMembers],
            ISNULL(N.[NoRoleNewMembers], 0) AS [NoRoleNewMembers]
        FROM (
            SELECT
                COUNT(*) AS [NoRoleMembers],
                SUM(CASE WHEN M.[MemberCreated] >= @From AND M.[MemberCreated] < @ToExclusive THEN 1 ELSE 0 END) AS [NoRoleNewMembers]
            FROM [CMS_Member] M
            WHERE NOT EXISTS (
                SELECT 1 FROM [CMS_MemberRoleMember] RM WHERE RM.[MemberRoleMemberMemberID] = M.[MemberID])
        ) N;
        """;

    // 3. Top roles by current members (roles without members too), with the number of roles. Window counts run before TOP.
    private const string RolesQuery = """
        SELECT TOP (@Limit)
            R.[MemberRoleID],
            R.[MemberRoleDisplayName],
            COUNT(M.[MemberID]) AS [MemberCount],
            SUM(CASE WHEN M.[MemberCreated] >= @From AND M.[MemberCreated] < @ToExclusive THEN 1 ELSE 0 END) AS [NewMemberCount],
            COUNT(*) OVER () AS [RoleCount]
        FROM [CMS_MemberRole] R
        LEFT JOIN [CMS_MemberRoleMember] RM ON RM.[MemberRoleMemberMemberRoleID] = R.[MemberRoleID]
        LEFT JOIN [CMS_Member] M ON M.[MemberID] = RM.[MemberRoleMemberMemberID]
        GROUP BY R.[MemberRoleID], R.[MemberRoleDisplayName]
        ORDER BY [MemberCount] DESC, R.[MemberRoleDisplayName], R.[MemberRoleID];
        """;

    /// <summary>
    /// Builds the batch (see the class remarks).
    /// </summary>
    public static string BuildReport() =>
        string.Join(Environment.NewLine, "SET NOCOUNT ON;", availabilityCheck, DailyQuery, TotalsQuery, RolesQuery);
}
