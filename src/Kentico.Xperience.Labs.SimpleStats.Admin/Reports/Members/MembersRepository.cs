using System.Data;
using System.Data.Common;

using CMS.DataEngine;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Members;

/// <summary>
/// Reads aggregated member data from the database.
/// </summary>
internal interface IMembersRepository
{
    /// <summary>
    /// Returns members created per day from <paramref name="previousFrom"/> to <paramref name="to"/>, totals and top member roles.
    /// <see cref="MembersReportData.Unavailable"/> when the member tables do not exist.
    /// </summary>
    /// <param name="previousFrom">First day of the previous period (inclusive).</param>
    /// <param name="from">First day of the range (inclusive).</param>
    /// <param name="to">Last day of the range (inclusive).</param>
    /// <param name="limit">Maximum number of member roles.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<MembersReportData> GetData(DateOnly previousFrom, DateOnly from, DateOnly to, int limit, CancellationToken cancellationToken);
}

internal sealed class MembersRepository : IMembersRepository
{
    private const string ReportName = "members";

    public async Task<MembersReportData> GetData(DateOnly previousFrom, DateOnly from, DateOnly to, int limit, CancellationToken cancellationToken)
    {
        var parameters = new QueryDataParameters
        {
            new DataParameter(MembersSql.PreviousFromParameter, previousFrom.ToDateTime(TimeOnly.MinValue)),
            new DataParameter(MembersSql.FromParameter, from.ToDateTime(TimeOnly.MinValue)),
            new DataParameter(MembersSql.ToExclusiveParameter, to.AddDays(1).ToDateTime(TimeOnly.MinValue)),
            new DataParameter(MembersSql.LimitParameter, limit),
        };

        await using var reader = await ConnectionHelper.ExecuteReaderAsync(
            MembersSql.BuildReport(),
            parameters,
            QueryTypeEnum.SQLQuery,
            CommandBehavior.Default,
            cancellationToken);

        if (!await StatsSql.IsAvailable(reader, MembersSql.AvailableColumn, cancellationToken))
        {
            return MembersReportData.Unavailable;
        }

        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var daily = await ReadDaily(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var totals = await ReadTotals(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var (roles, roleCount) = await ReadRoles(reader, cancellationToken);

        return new(true, daily, totals, roles, roleCount);
    }

    private static async Task<IReadOnlyList<MembersDailyRow>> ReadDaily(DbDataReader reader, CancellationToken cancellationToken)
    {
        int dateOrdinal = reader.GetOrdinal(MembersSql.DateColumn);
        int internalOrdinal = reader.GetOrdinal(MembersSql.InternalColumn);
        int externalOrdinal = reader.GetOrdinal(MembersSql.ExternalColumn);

        var rows = new List<MembersDailyRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                DateOnly.FromDateTime(reader.GetDateTime(dateOrdinal)),
                reader.GetInt32(internalOrdinal),
                reader.GetInt32(externalOrdinal)));
        }

        return rows;
    }

    private static async Task<MembersTotalsRow> ReadTotals(DbDataReader reader, CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken))
        {
            return MembersTotalsRow.Empty;
        }

        int Int(string column) => reader.GetInt32(reader.GetOrdinal(column));

        return new(
            Int(MembersSql.MembersBeforeColumn),
            Int(MembersSql.AllMembersColumn),
            Int(MembersSql.DisabledColumn),
            Int(MembersSql.NoRoleColumn),
            Int(MembersSql.NoRoleNewColumn));
    }

    private static async Task<(IReadOnlyList<MembersRoleRow> Rows, int RoleCount)> ReadRoles(DbDataReader reader, CancellationToken cancellationToken)
    {
        int idOrdinal = reader.GetOrdinal(MembersSql.RoleIdColumn);
        int nameOrdinal = reader.GetOrdinal(MembersSql.RoleNameColumn);
        int membersOrdinal = reader.GetOrdinal(MembersSql.MembersColumn);
        int newOrdinal = reader.GetOrdinal(MembersSql.NewMembersColumn);
        int countOrdinal = reader.GetOrdinal(MembersSql.RoleCountColumn);

        var rows = new List<MembersRoleRow>();
        int roleCount = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.GetInt32(idOrdinal),
                reader.IsDBNull(nameOrdinal) ? null : reader.GetString(nameOrdinal),
                reader.GetInt32(membersOrdinal),
                reader.GetInt32(newOrdinal)));

            // Same value on every row.
            roleCount = reader.GetInt32(countOrdinal);
        }

        return (rows, roleCount);
    }
}
