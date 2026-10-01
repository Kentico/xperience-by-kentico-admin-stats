using System.Globalization;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Members;

/// <summary>
/// Turns aggregated member data into the member registrations report.
/// </summary>
internal static class MembersReportBuilder
{
    /// <summary>
    /// Member roles read from the database. The list also has the "No role" row (sorted by members like the roles).
    /// </summary>
    public const int RoleLimit = 10;

    /// <summary>
    /// Key of the "No role" row. Contains a character that role IDs cannot contain, so it does not collide with role keys.
    /// </summary>
    public const string NoRoleKey = "role:(none)";

    /// <summary>
    /// Label of members without a role.
    /// </summary>
    public const string NoRoleLabel = "No role";

    public static StatsSeriesDefinition NewMembersSeries { get; } = new("new", "New members");

    public static StatsSeriesDefinition InternalSeries { get; } = new("internal", "Internal");

    public static StatsSeriesDefinition ExternalSeries { get; } = new("external", "External");

    public static StatsSeriesDefinition TotalMembersSeries { get; } = new("total", "Total members");

    /// <summary>
    /// Builds the report.
    /// </summary>
    /// <param name="range">Normalized filter. The channel is ignored.</param>
    /// <param name="data">Data from the start of the previous period (see <see cref="StatsComparison.GetPreviousRange"/>) to the end of the range.</param>
    /// <param name="getRolePath">Returns the admin path of a member role, or <c>null</c>.</param>
    public static MembersResult Build(StatsQuery range, MembersReportData data, Func<int, string?>? getRolePath = null)
    {
        range = range with { ChannelId = null };
        var (previousFrom, previousTo) = StatsComparison.GetPreviousRange(range);
        var totals = data.Totals;

        var internalValues = data.Daily.Select(row => new StatsDailyValue(InternalSeries.Key, row.Date, row.Internal)).ToList();
        var externalValues = data.Daily.Select(row => new StatsDailyValue(ExternalSeries.Key, row.Date, row.External)).ToList();
        var newValues = data.Daily
            .Select(row => new StatsDailyValue(NewMembersSeries.Key, row.Date, Math.Max(row.Internal, 0) + Math.Max(row.External, 0)))
            .ToList();

        // Rows before the range are the previous period; the series use only the range.
        var newMembers = StatsTimeSeriesBuilder.BuildValueSeries(range, newValues, NewMembersSeries, StatsValueKind.Count);
        var internalMembers = StatsTimeSeriesBuilder.BuildValueSeries(range, internalValues, InternalSeries, StatsValueKind.Count);
        var externalMembers = StatsTimeSeriesBuilder.BuildValueSeries(range, externalValues, ExternalSeries, StatsValueKind.Count);

        int membersBefore = Math.Max(totals.MembersBeforeRange, 0);
        var totalMembers = StatsTimeSeriesBuilder.BuildCumulativeSeries(
            range,
            newValues.Select(row => row with { SeriesKey = TotalMembersSeries.Key }),
            TotalMembersSeries,
            StatsValueKind.Count,
            membersBefore);

        (int New, int External) Sum(DateOnly from, DateOnly to)
        {
            var rows = data.Daily.Where(row => row.Date >= from && row.Date <= to).ToList();
            int external = rows.Sum(row => Math.Max(row.External, 0));
            return (rows.Sum(row => Math.Max(row.Internal, 0)) + external, external);
        }

        var (newCount, externalCount) = Sum(range.From, range.To);
        var (previousNew, previousExternal) = Sum(previousFrom, previousTo);

        var kpis = new MembersTotals(
            StatsValueComparison.Create(range, StatsValueKind.Count, newCount, previousNew),
            // Point in time: members on the range end vs on the previous period end (= members created before the range).
            StatsValueComparison.Create(range, StatsValueKind.Count, totalMembers.Total, membersBefore),
            StatsValueComparison.Create(
                range,
                StatsValueKind.Ratio,
                StatsValues.Divide(externalCount, newCount),
                StatsValues.Divide(previousExternal, previousNew)),
            Math.Max(totals.DisabledMembers, 0));

        return new(
            range.From,
            range.To,
            range.Grouping,
            StatsPeriods.Build(range.From, range.To, range.Grouping),
            newMembers,
            internalMembers,
            externalMembers,
            totalMembers,
            kpis,
            BuildRoles(data, getRolePath),
            data.Available);
    }

    /// <summary>
    /// Current members per role plus the "No role" row. A member can have several roles, so shares are of all members.
    /// </summary>
    private static StatsRankedResult BuildRoles(MembersReportData data, Func<int, string?>? getRolePath)
    {
        var totals = data.Totals;

        var entries = data.Roles
            .Select(row => new StatsRankedEntry(
                Key: string.Create(CultureInfo.InvariantCulture, $"role:{row.RoleId}"),
                Label: string.IsNullOrWhiteSpace(row.DisplayName)
                    ? string.Create(CultureInfo.InvariantCulture, $"Role #{row.RoleId}")
                    : row.DisplayName.Trim(),
                SecondaryLabel: null,
                Value: Math.Max(row.Members, 0),
                SecondaryValue: Math.Max(row.NewMembers, 0),
                Url: null)
            {
                AdminPath = getRolePath?.Invoke(row.RoleId),
            })
            .Append(new StatsRankedEntry(
                NoRoleKey,
                NoRoleLabel,
                null,
                Math.Max(totals.NoRoleMembers, 0),
                Math.Max(totals.NoRoleNewMembers, 0),
                null));

        int itemCount = Math.Max(data.RoleCount, 0) + (totals.NoRoleMembers > 0 ? 1 : 0);

        return StatsRankedBuilder.BuildSnapshot(
            channelId: null,
            entries,
            Math.Max(totals.AllMembers, 0),
            itemCount,
            RoleLimit + 1,
            itemsOverlap: true);
    }
}
