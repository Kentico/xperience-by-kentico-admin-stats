using System.Data;
using System.Data.Common;

using CMS.DataEngine;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EventLog;

/// <summary>
/// Reads aggregated event log data from the database.
/// </summary>
internal interface IEventLogRepository
{
    /// <summary>
    /// Returns daily counts per type from <paramref name="previousFrom"/> to <paramref name="to"/>, and the top sources,
    /// event codes and users (with <paramref name="eventType"/> applied).
    /// </summary>
    /// <param name="previousFrom">First day of the previous period (inclusive).</param>
    /// <param name="from">First day of the range (inclusive).</param>
    /// <param name="to">Last day of the range (inclusive).</param>
    /// <param name="eventType">Optional event type code.</param>
    /// <param name="limit">Maximum number of rows of each top list.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<EventLogReportData> GetData(DateOnly previousFrom, DateOnly from, DateOnly to, string? eventType, int limit, CancellationToken cancellationToken);
}

internal sealed class EventLogRepository : IEventLogRepository
{
    public async Task<EventLogReportData> GetData(DateOnly previousFrom, DateOnly from, DateOnly to, string? eventType, int limit, CancellationToken cancellationToken)
    {
        var parameters = new QueryDataParameters
        {
            new DataParameter(EventLogSql.PreviousFromParameter, previousFrom.ToDateTime(TimeOnly.MinValue)),
            new DataParameter(EventLogSql.FromParameter, from.ToDateTime(TimeOnly.MinValue)),
            new DataParameter(EventLogSql.ToExclusiveParameter, to.AddDays(1).ToDateTime(TimeOnly.MinValue)),
            new DataParameter(EventLogSql.LimitParameter, limit),
        };
        if (eventType is not null)
        {
            parameters.Add(new DataParameter(EventLogSql.EventTypeParameter, eventType));
        }

        var prefixes = EventLogSources.XperiencePrefixes;
        for (int i = 0; i < prefixes.Count; i++)
        {
            parameters.Add(new DataParameter(EventLogSql.GetSourcePrefixParameter(i), EventLogSources.ToLikePattern(prefixes[i])));
        }

        var names = EventLogSources.XperienceNames;
        for (int i = 0; i < names.Count; i++)
        {
            parameters.Add(new DataParameter(EventLogSql.GetSourceExactParameter(i), names[i]));
        }

        string sql = EventLogSql.Build(eventType is not null, prefixes.Count, names.Count);

        await using var reader = await ConnectionHelper.ExecuteReaderAsync(sql, parameters, QueryTypeEnum.SQLQuery, CommandBehavior.Default, cancellationToken);

        var daily = await ReadDaily(reader, cancellationToken);
        await NextResult(reader, cancellationToken);
        var sources = await ReadGroups(reader, cancellationToken);
        await NextResult(reader, cancellationToken);
        var xperienceSources = await ReadGroups(reader, cancellationToken);
        await NextResult(reader, cancellationToken);
        var customSources = await ReadGroups(reader, cancellationToken);
        await NextResult(reader, cancellationToken);
        var codes = await ReadGroups(reader, cancellationToken);
        await NextResult(reader, cancellationToken);
        var users = await ReadUsers(reader, cancellationToken);

        return new(daily, sources, codes, users)
        {
            XperienceSources = xperienceSources,
            CustomSources = customSources,
        };
    }

    private static async Task NextResult(DbDataReader reader, CancellationToken cancellationToken)
    {
        if (!await reader.NextResultAsync(cancellationToken))
        {
            throw new InvalidOperationException("The event log query returned fewer result sets than expected.");
        }
    }

    private static async Task<IReadOnlyList<EventLogDailyCount>> ReadDaily(DbDataReader reader, CancellationToken cancellationToken)
    {
        int typeOrdinal = reader.GetOrdinal(EventLogSql.EventTypeColumn);
        int dateOrdinal = reader.GetOrdinal(EventLogSql.DateColumn);
        int countOrdinal = reader.GetOrdinal(EventLogSql.CountColumn);

        var rows = new List<EventLogDailyCount>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.GetString(typeOrdinal),
                DateOnly.FromDateTime(reader.GetDateTime(dateOrdinal)),
                reader.GetInt32(countOrdinal)));
        }

        return rows;
    }

    private static async Task<EventLogTop<EventLogGroupRow>> ReadGroups(DbDataReader reader, CancellationToken cancellationToken)
    {
        int keyOrdinal = reader.GetOrdinal(EventLogSql.GroupKeyColumn);
        int currentOrdinal = reader.GetOrdinal(EventLogSql.CurrentColumn);
        int previousOrdinal = reader.GetOrdinal(EventLogSql.PreviousColumn);
        int groupCountOrdinal = reader.GetOrdinal(EventLogSql.GroupCountColumn);

        var rows = new List<EventLogGroupRow>();
        int groupCount = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.GetString(keyOrdinal),
                reader.GetInt32(currentOrdinal),
                reader.GetInt32(previousOrdinal)));

            // Same value on every row.
            groupCount = reader.GetInt32(groupCountOrdinal);
        }

        return new(rows, groupCount);
    }

    private static async Task<EventLogTop<EventLogUserRow>> ReadUsers(DbDataReader reader, CancellationToken cancellationToken)
    {
        int idOrdinal = reader.GetOrdinal(EventLogSql.UserIdColumn);
        int nameOrdinal = reader.GetOrdinal(EventLogSql.UserNameColumn);
        int currentOrdinal = reader.GetOrdinal(EventLogSql.CurrentColumn);
        int previousOrdinal = reader.GetOrdinal(EventLogSql.PreviousColumn);
        int existsOrdinal = reader.GetOrdinal(EventLogSql.UserExistsColumn);
        int groupCountOrdinal = reader.GetOrdinal(EventLogSql.GroupCountColumn);

        var rows = new List<EventLogUserRow>();
        int groupCount = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.IsDBNull(idOrdinal) ? null : reader.GetInt32(idOrdinal),
                reader.IsDBNull(nameOrdinal) ? null : reader.GetString(nameOrdinal),
                reader.GetBoolean(existsOrdinal),
                reader.GetInt32(currentOrdinal),
                reader.GetInt32(previousOrdinal)));

            groupCount = reader.GetInt32(groupCountOrdinal);
        }

        return new(rows, groupCount);
    }
}
