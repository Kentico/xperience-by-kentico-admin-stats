using System.Data;

using CMS.Activities;
using CMS.DataEngine;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ActivityCounts;

/// <summary>
/// Reads aggregated activity counts from the database.
/// </summary>
internal interface IActivityCountsRepository
{
    /// <summary>
    /// Returns activity counts grouped by activity type and day.
    /// </summary>
    public Task<IReadOnlyList<ActivityDailyCount>> GetDailyCounts(DateOnly from, DateOnly to, int? channelId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns activity type display names keyed by code name (case-insensitive).
    /// </summary>
    public Task<IReadOnlyDictionary<string, string>> GetActivityTypeDisplayNames(CancellationToken cancellationToken);
}

internal sealed class ActivityCountsRepository(IInfoProvider<ActivityTypeInfo> activityTypeProvider) : IActivityCountsRepository
{
    private readonly IInfoProvider<ActivityTypeInfo> activityTypeProvider = activityTypeProvider;

    // Only constant SQL fragments are combined here. All values are passed as parameters.
    private const string SelectClause = """
        SELECT
            A.[ActivityType] AS [ActivityType],
            CAST(A.[ActivityCreated] AS date) AS [ActivityDate],
            COUNT(*) AS [ActivityCount]
        FROM [OM_Activity] A
        WHERE A.[ActivityCreated] >= @From
            AND A.[ActivityCreated] < @ToExclusive
        """;

    private const string ChannelClause = """

            AND A.[ActivityChannelID] = @ChannelID
        """;

    private const string GroupByClause = """

        GROUP BY A.[ActivityType], CAST(A.[ActivityCreated] AS date)
        """;

    public async Task<IReadOnlyList<ActivityDailyCount>> GetDailyCounts(DateOnly from, DateOnly to, int? channelId, CancellationToken cancellationToken)
    {
        var parameters = new QueryDataParameters
        {
            new DataParameter("@From", from.ToDateTime(TimeOnly.MinValue)),
            new DataParameter("@ToExclusive", to.AddDays(1).ToDateTime(TimeOnly.MinValue)),
        };

        string query = SelectClause;
        if (channelId is int id)
        {
            parameters.Add(new DataParameter("@ChannelID", id));
            query += ChannelClause;
        }

        query += GroupByClause;

        await using var reader = await ConnectionHelper.ExecuteReaderAsync(query, parameters, QueryTypeEnum.SQLQuery, CommandBehavior.Default, cancellationToken);

        var rows = new List<ActivityDailyCount>();
        int typeOrdinal = reader.GetOrdinal("ActivityType");
        int dateOrdinal = reader.GetOrdinal("ActivityDate");
        int countOrdinal = reader.GetOrdinal("ActivityCount");

        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.IsDBNull(typeOrdinal) ? string.Empty : reader.GetString(typeOrdinal),
                DateOnly.FromDateTime(reader.GetDateTime(dateOrdinal)),
                reader.GetInt32(countOrdinal)));
        }

        return rows;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetActivityTypeDisplayNames(CancellationToken cancellationToken)
    {
        var types = await activityTypeProvider
            .Get()
            .Columns(nameof(ActivityTypeInfo.ActivityTypeName), nameof(ActivityTypeInfo.ActivityTypeDisplayName))
            .GetEnumerableTypedResultAsync(cancellationToken: cancellationToken);

        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in types)
        {
            names[type.ActivityTypeName] = string.IsNullOrWhiteSpace(type.ActivityTypeDisplayName)
                ? type.ActivityTypeName
                : type.ActivityTypeDisplayName;
        }

        return names;
    }
}
