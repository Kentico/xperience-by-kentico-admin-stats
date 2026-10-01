using CMS.Membership;

using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Members;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class MembersSqlTests
{
    [Test]
    public void BuildReport_ChecksTablesFirst_AndReturnsEarly()
    {
        string sql = MembersSql.BuildReport();

        string[] tables = ["CMS_Member", "CMS_MemberRole", "CMS_MemberRoleMember"];
        Assert.That(tables, Is.All.Matches<string>(table => sql.Contains($"OBJECT_ID(N'[{table}]', N'U') IS NULL", StringComparison.Ordinal)));
        Assert.That(sql, Does.Contain($"SELECT CAST(0 AS bit) AS [{MembersSql.AvailableColumn}];"));
        Assert.That(sql.IndexOf("RETURN;", StringComparison.Ordinal), Is.LessThan(sql.IndexOf("FROM [CMS_Member] M", StringComparison.Ordinal)));
    }

    [Test]
    public void BuildReport_UsesParameters()
    {
        string sql = MembersSql.BuildReport();

        Assert.That(sql, Does.Contain("M.[MemberCreated] >= @PreviousFrom"));
        Assert.That(sql, Does.Contain("M.[MemberCreated] < @From"));
        Assert.That(sql, Does.Contain("M.[MemberCreated] < @ToExclusive"));
        Assert.That(sql, Does.Contain("TOP (@Limit)"));
    }

    [Test]
    public void BuildReport_MembersWithoutRoles_AndRolesWithoutMembers()
    {
        string sql = MembersSql.BuildReport();

        Assert.That(sql, Does.Contain("WHERE NOT EXISTS ("));
        Assert.That(sql, Does.Contain("LEFT JOIN [CMS_MemberRoleMember] RM"));
        // An empty "no role" group sums to NULL.
        Assert.That(sql, Does.Contain("ISNULL(N.[NoRoleNewMembers], 0)"));
    }

    [Test]
    public void BuildReport_DisabledAndExternal()
    {
        string sql = MembersSql.BuildReport();

        Assert.That(sql, Does.Contain("M.[MemberEnabled] = 0"));
        Assert.That(sql, Does.Contain("CASE WHEN M.[MemberIsExternal] = 1 THEN 1 ELSE 0 END"));
    }

    /// <summary>
    /// The SQL reads these columns; the product's Info classes must still have them.
    /// </summary>
    [Test]
    public void Sql_UsesColumnsOfTheInfoClasses()
    {
        string sql = MembersSql.BuildReport();

        string[] columns =
        [
            nameof(MemberInfo.MemberID),
            nameof(MemberInfo.MemberCreated),
            nameof(MemberInfo.MemberEnabled),
            nameof(MemberInfo.MemberIsExternal),
            nameof(MemberRoleInfo.MemberRoleID),
            nameof(MemberRoleInfo.MemberRoleDisplayName),
            nameof(MemberRoleMemberInfo.MemberRoleMemberMemberID),
            nameof(MemberRoleMemberInfo.MemberRoleMemberMemberRoleID),
        ];

        Assert.That(columns, Is.All.Matches<string>(column => sql.Contains($"[{column}]", StringComparison.Ordinal)));
    }

    [Test]
    public void AvailabilityCheck_UsesGivenColumn()
    {
        string sql = StatsSql.BuildAvailabilityCheck("X", "A", "B");

        Assert.That(sql, Does.StartWith("IF OBJECT_ID(N'[A]', N'U') IS NULL"));
        Assert.That(sql, Does.Contain("OR OBJECT_ID(N'[B]', N'U') IS NULL"));
        Assert.That(sql, Does.Contain("SELECT CAST(0 AS bit) AS [X];"));
        Assert.That(sql, Does.Contain("SELECT CAST(1 AS bit) AS [X];"));
    }
}
