namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EventLog;

/// <summary>
/// Builds the event log batch: all aggregates in one round trip. Only constant SQL fragments are combined; all values are parameters.
/// </summary>
/// <remarks>
/// Result sets, in order:
/// <list type="number">
/// <item>Events per type per day from the start of the previous period to the end of the range (all types).</item>
/// <item>Top sources: events in the range and in the previous period (type filter applied).</item>
/// <item>Top Xperience sources (<see cref="EventLogSources.XperiencePrefixes"/>): same as sources.</item>
/// <item>Top custom sources (all other sources): same as sources.</item>
/// <item>Top event codes: same as sources.</item>
/// <item>Top users: events in the range and in the previous period (type filter applied). Events without a user ID are grouped by user name (usually empty: system events).</item>
/// </list>
/// <c>CMS_EventLog</c> has no index on <c>EventTime</c> (only the primary key), so each statement scans the table.
/// That is fine: the table is capped by the "Event log size" setting (<c>CMSLogSize</c>).
/// </remarks>
internal static class EventLogSql
{
    /// <summary>Start of the previous period (inclusive).</summary>
    public const string PreviousFromParameter = "@PreviousFrom";

    /// <summary>Start of the range (inclusive). Earlier events belong to the previous period.</summary>
    public const string FromParameter = "@From";

    /// <summary>Day after the end of the range (exclusive).</summary>
    public const string ToExclusiveParameter = "@ToExclusive";

    /// <summary>Event type code of the type filter.</summary>
    public const string EventTypeParameter = "@EventType";

    /// <summary>Maximum number of rows of each top list.</summary>
    public const string LimitParameter = "@Limit";

    /// <summary>Prefix of the <c>LIKE</c> pattern parameters of <see cref="EventLogSources.XperiencePrefixes"/> (<c>@SourcePrefix0</c>, ...).</summary>
    public const string SourcePrefixParameter = "@SourcePrefix";

    /// <summary>Prefix of the parameters of <see cref="EventLogSources.XperienceNames"/> (<c>@SourceExact0</c>, ...).</summary>
    public const string SourceExactParameter = "@SourceExact";

    public const string EventTypeColumn = "EventType";
    public const string DateColumn = "EventDate";
    public const string CountColumn = "EventCount";
    public const string GroupKeyColumn = "GroupKey";
    public const string CurrentColumn = "CurrentCount";
    public const string PreviousColumn = "PreviousCount";
    public const string GroupCountColumn = "GroupCount";
    public const string UserIdColumn = "UserID";
    public const string UserNameColumn = "UserName";
    public const string UserExistsColumn = "UserExists";

    private const string TypeCondition = """

            AND [EventType] = @EventType
        """;

    // 1. Daily counts per type, previous period + range.
    private const string DailyQuery = """
        SELECT
            [EventType],
            CAST([EventTime] AS date) AS [EventDate],
            COUNT(*) AS [EventCount]
        FROM [CMS_EventLog]
        WHERE [EventTime] >= @PreviousFrom
            AND [EventTime] < @ToExclusive
        GROUP BY [EventType], CAST([EventTime] AS date);
        """;

    // 2.-5. Top groups ({0} = column, {1} = type condition, {2} = source kind condition). COUNT(*) OVER () runs after HAVING and before TOP,
    // so it counts every group with events in the range.
    private const string TopGroupQuery = """
        SELECT TOP (@Limit)
            [{0}] AS [GroupKey],
            SUM(CASE WHEN [EventTime] >= @From THEN 1 ELSE 0 END) AS [CurrentCount],
            SUM(CASE WHEN [EventTime] < @From THEN 1 ELSE 0 END) AS [PreviousCount],
            COUNT(*) OVER () AS [GroupCount]
        FROM [CMS_EventLog]
        WHERE [EventTime] >= @PreviousFrom
            AND [EventTime] < @ToExclusive{1}{2}
        GROUP BY [{0}]
        HAVING SUM(CASE WHEN [EventTime] >= @From THEN 1 ELSE 0 END) > 0
        ORDER BY [CurrentCount] DESC, [GroupKey];
        """;

    // 4. Top users ({0} = type condition), same counts as the top groups. One row per user ID (latest-looking name via MAX);
    // events without a user ID are grouped by their trimmed name (empty = system).
    private const string TopUsersQuery = """
        SELECT TOP (@Limit)
            E.[UserID],
            MAX(E.[UserName]) AS [UserName],
            SUM(CASE WHEN E.[EventTime] >= @From THEN 1 ELSE 0 END) AS [CurrentCount],
            SUM(CASE WHEN E.[EventTime] < @From THEN 1 ELSE 0 END) AS [PreviousCount],
            CAST(CASE WHEN EXISTS (SELECT 1 FROM [CMS_User] U WHERE U.[UserID] = E.[UserID]) THEN 1 ELSE 0 END AS bit) AS [UserExists],
            COUNT(*) OVER () AS [GroupCount]
        FROM (
            SELECT [UserID], NULLIF(LTRIM(RTRIM([UserName])), N'') AS [UserName], [EventTime]
            FROM [CMS_EventLog]
            WHERE [EventTime] >= @PreviousFrom
                AND [EventTime] < @ToExclusive{0}
        ) E
        GROUP BY E.[UserID], CASE WHEN E.[UserID] IS NULL THEN E.[UserName] END
        HAVING SUM(CASE WHEN E.[EventTime] >= @From THEN 1 ELSE 0 END) > 0
        ORDER BY [CurrentCount] DESC, [UserName];
        """;

    /// <summary>
    /// Name of the <c>LIKE</c> pattern parameter of the Xperience source prefix at <paramref name="index"/>.
    /// </summary>
    public static string GetSourcePrefixParameter(int index) => $"{SourcePrefixParameter}{index}";

    /// <summary>
    /// Name of the parameter of the exact Xperience source name at <paramref name="index"/>.
    /// </summary>
    public static string GetSourceExactParameter(int index) => $"{SourceExactParameter}{index}";

    /// <summary>
    /// Builds the batch. With <paramref name="filterByType"/>, the top lists read only events of <see cref="EventTypeParameter"/>.
    /// </summary>
    /// <param name="filterByType">Whether the type filter applies.</param>
    /// <param name="sourcePrefixCount">
    /// Number of Xperience source prefix parameters (<see cref="GetSourcePrefixParameter"/>).
    /// </param>
    /// <param name="sourceExactCount">
    /// Number of exact Xperience source name parameters (<see cref="GetSourceExactParameter"/>). With no prefixes and names, every source is custom.
    /// </param>
    public static string Build(bool filterByType, int sourcePrefixCount, int sourceExactCount)
    {
        string type = filterByType ? TypeCondition : string.Empty;

        var conditions = Enumerable.Range(0, Math.Max(sourcePrefixCount, 0))
            .Select(i => $"[Source] LIKE {GetSourcePrefixParameter(i)} ESCAPE N'{EventLogSources.LikeEscape}'")
            .Concat(Enumerable.Range(0, Math.Max(sourceExactCount, 0)).Select(i => $"[Source] = {GetSourceExactParameter(i)}"))
            .ToList();

        // Source is NOT NULL, so NOT (...) selects exactly the other sources.
        string xperience = conditions.Count > 0 ? "(" + string.Join(" OR ", conditions) + ")" : "1 = 0";

        return string.Join(
            Environment.NewLine,
            "SET NOCOUNT ON;",
            DailyQuery,
            string.Format(TopGroupQuery, "Source", type, string.Empty),
            string.Format(TopGroupQuery, "Source", type, $"{Environment.NewLine}            AND {xperience}"),
            string.Format(TopGroupQuery, "Source", type, $"{Environment.NewLine}            AND NOT {xperience}"),
            string.Format(TopGroupQuery, "EventCode", type, string.Empty),
            string.Format(TopUsersQuery, type));
    }
}
