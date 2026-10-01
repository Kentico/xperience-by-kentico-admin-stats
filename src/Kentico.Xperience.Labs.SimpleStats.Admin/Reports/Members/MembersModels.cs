using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Members;

/// <summary>
/// KPIs of the range compared with the previous period.
/// </summary>
/// <param name="NewMembers">Members created in the period that still exist.</param>
/// <param name="TotalMembers">
/// Members created on or before the last day of the range vs on or before the last day of the previous period (point in time, not a sum).
/// Deleted members are not counted, also not for past dates.
/// </param>
/// <param name="ExternalShare">
/// Share (ratio) of new members that signed up through an external sign-in provider. <c>null</c> for a period without new members.
/// The change is in percentage points.
/// </param>
/// <param name="DisabledMembers">Members that are disabled now (current state, not historical, so no comparison).</param>
public sealed record MembersTotals(
    StatsValueComparison NewMembers,
    StatsValueComparison TotalMembers,
    StatsValueComparison ExternalShare,
    int DisabledMembers);

/// <summary>
/// Member registrations report.
/// </summary>
/// <param name="From">Applied range start (inclusive).</param>
/// <param name="To">Applied range end (inclusive).</param>
/// <param name="Grouping">Applied grouping.</param>
/// <param name="Periods">Period axis of the series.</param>
/// <param name="NewMembers">Members created per period (internal + external).</param>
/// <param name="InternalMembers">Members created per period without an external sign-in provider.</param>
/// <param name="ExternalMembers">Members created per period through an external sign-in provider.</param>
/// <param name="TotalMembers">Members at the end of each period (running total, see <see cref="StatsTimeSeriesBuilder.BuildCumulativeSeries"/>).</param>
/// <param name="Totals">KPIs vs the previous period.</param>
/// <param name="ByRole">
/// Current members per member role (current state), plus a "No role" row for members without a role. Value = members now,
/// secondary value = of them created in the range. A member can have several roles; shares are of all members.
/// </param>
/// <param name="Available"><c>false</c> when the member tables do not exist; the report is then empty.</param>
public sealed record MembersResult(
    DateOnly From,
    DateOnly To,
    StatsGrouping Grouping,
    IReadOnlyList<StatsPeriod> Periods,
    StatsValueSeries NewMembers,
    StatsValueSeries InternalMembers,
    StatsValueSeries ExternalMembers,
    StatsValueSeries TotalMembers,
    MembersTotals Totals,
    StatsRankedResult ByRole,
    bool Available)
{
    /// <summary>
    /// Path of the native Members application's listing, relative to the admin root. <c>null</c> when it is not available.
    /// </summary>
    public string? MembersPath { get; init; }

    /// <summary>
    /// When the data was read from the database. Can be older than the request when served from cache.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Members created on one day, as returned by the SQL aggregate.
/// </summary>
/// <param name="Date">Day the members were created.</param>
/// <param name="Internal">Members without an external sign-in provider.</param>
/// <param name="External">Members created through an external sign-in provider.</param>
public sealed record MembersDailyRow(DateOnly Date, int Internal, int External);

/// <summary>
/// Member totals (current state, except <paramref name="MembersBeforeRange"/>).
/// </summary>
/// <param name="MembersBeforeRange">Members created before the range start (start of the total members line).</param>
/// <param name="AllMembers">All members now.</param>
/// <param name="DisabledMembers">Disabled members now.</param>
/// <param name="NoRoleMembers">Members without a role now.</param>
/// <param name="NoRoleNewMembers">Members without a role now that were created in the range.</param>
public sealed record MembersTotalsRow(
    int MembersBeforeRange,
    int AllMembers,
    int DisabledMembers,
    int NoRoleMembers,
    int NoRoleNewMembers)
{
    public static MembersTotalsRow Empty { get; } = new(0, 0, 0, 0, 0);
}

/// <summary>
/// Current members of one member role.
/// </summary>
/// <param name="RoleId">Member role ID.</param>
/// <param name="DisplayName">Member role display name.</param>
/// <param name="Members">Members in the role now.</param>
/// <param name="NewMembers">Of them, members created in the range.</param>
public sealed record MembersRoleRow(int RoleId, string? DisplayName, int Members, int NewMembers);

/// <summary>
/// Aggregated member data. <see cref="Daily"/> starts at the previous period.
/// </summary>
/// <param name="Available"><c>false</c> when the member tables do not exist.</param>
/// <param name="Daily">Members created per day, previous period + range.</param>
/// <param name="Totals">Member totals.</param>
/// <param name="Roles">Top member roles by current members.</param>
/// <param name="RoleCount">Number of member roles.</param>
public sealed record MembersReportData(
    bool Available,
    IReadOnlyList<MembersDailyRow> Daily,
    MembersTotalsRow Totals,
    IReadOnlyList<MembersRoleRow> Roles,
    int RoleCount)
{
    public static MembersReportData Empty { get; } = new(true, [], MembersTotalsRow.Empty, [], 0);

    public static MembersReportData Unavailable { get; } = Empty with { Available = false };
}

/// <summary>
/// Member data with the time it was read. This is the cached value.
/// </summary>
internal sealed record MembersSnapshot(MembersReportData Data, DateTimeOffset ReadAt);
