using System.Data;
using System.Data.Common;

using CMS.DataEngine;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.RecipientLists;

/// <summary>
/// Reads aggregated recipient list data from the database.
/// </summary>
internal interface IRecipientListsRepository
{
    /// <summary>
    /// Returns all recipient lists with their numbers, events per day from <paramref name="previousFrom"/> to <paramref name="to"/>
    /// and totals. <see cref="RecipientListsReportData.Unavailable"/> when the tables do not exist.
    /// </summary>
    /// <param name="previousFrom">First day of the previous period (inclusive).</param>
    /// <param name="from">First day of the range (inclusive).</param>
    /// <param name="to">Last day of the range (inclusive).</param>
    /// <param name="listId">List filter, <c>null</c> for all lists. An ID that is not a recipient list means all lists.</param>
    /// <param name="softBounceLimit">Soft bounces after which an email counts as bounced.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<RecipientListsReportData> GetData(
        DateOnly previousFrom,
        DateOnly from,
        DateOnly to,
        int? listId,
        int softBounceLimit,
        CancellationToken cancellationToken);
}

internal sealed class RecipientListsRepository : IRecipientListsRepository
{
    private const string ReportName = "recipient lists";

    public async Task<RecipientListsReportData> GetData(
        DateOnly previousFrom,
        DateOnly from,
        DateOnly to,
        int? listId,
        int softBounceLimit,
        CancellationToken cancellationToken)
    {
        var parameters = new QueryDataParameters
        {
            new DataParameter(RecipientListsSql.PreviousFromParameter, previousFrom.ToDateTime(TimeOnly.MinValue)),
            new DataParameter(RecipientListsSql.FromParameter, from.ToDateTime(TimeOnly.MinValue)),
            new DataParameter(RecipientListsSql.ToExclusiveParameter, to.AddDays(1).ToDateTime(TimeOnly.MinValue)),
            new DataParameter(RecipientListsSql.ListIdParameter, listId ?? 0),
            new DataParameter(RecipientListsSql.SoftBounceLimitParameter, softBounceLimit),
        };

        await using var reader = await ConnectionHelper.ExecuteReaderAsync(
            RecipientListsSql.BuildReport(),
            parameters,
            QueryTypeEnum.SQLQuery,
            CommandBehavior.Default,
            cancellationToken);

        if (!await StatsSql.IsAvailable(reader, RecipientListsSql.AvailableColumn, cancellationToken))
        {
            return RecipientListsReportData.Unavailable;
        }

        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var lists = await ReadLists(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var daily = await ReadDaily(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        int subscribersBefore = await reader.ReadAsync(cancellationToken)
            ? reader.GetInt32(reader.GetOrdinal(RecipientListsSql.SubscribersBeforeColumn))
            : 0;

        return new(true, lists, daily, subscribersBefore);
    }

    private static async Task<IReadOnlyList<RecipientListsListRow>> ReadLists(DbDataReader reader, CancellationToken cancellationToken)
    {
        int idOrdinal = reader.GetOrdinal(RecipientListsSql.ListIdColumn);
        int nameOrdinal = reader.GetOrdinal(RecipientListsSql.ListNameColumn);
        int subscriptionsOrdinal = reader.GetOrdinal(RecipientListsSql.SubscriptionsColumn);
        int unsubscriptionsOrdinal = reader.GetOrdinal(RecipientListsSql.UnsubscriptionsColumn);
        int subscribersOrdinal = reader.GetOrdinal(RecipientListsSql.SubscribersColumn);
        int previousSubscribersOrdinal = reader.GetOrdinal(RecipientListsSql.PreviousSubscribersColumn);
        int receivingOrdinal = reader.GetOrdinal(RecipientListsSql.ReceivingColumn);
        int bouncedOrdinal = reader.GetOrdinal(RecipientListsSql.BouncedColumn);
        int unsubscribedOrdinal = reader.GetOrdinal(RecipientListsSql.UnsubscribedColumn);
        int notConfirmedOrdinal = reader.GetOrdinal(RecipientListsSql.NotConfirmedColumn);

        var rows = new List<RecipientListsListRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.GetInt32(idOrdinal),
                reader.IsDBNull(nameOrdinal) ? null : reader.GetString(nameOrdinal),
                reader.GetInt32(subscriptionsOrdinal),
                reader.GetInt32(unsubscriptionsOrdinal),
                reader.GetInt32(subscribersOrdinal),
                reader.GetInt32(previousSubscribersOrdinal),
                new RecipientListStatuses(
                    reader.GetInt32(receivingOrdinal),
                    reader.GetInt32(bouncedOrdinal),
                    reader.GetInt32(unsubscribedOrdinal),
                    reader.GetInt32(notConfirmedOrdinal))));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<RecipientListsDailyRow>> ReadDaily(DbDataReader reader, CancellationToken cancellationToken)
    {
        int dateOrdinal = reader.GetOrdinal(RecipientListsSql.DateColumn);
        int subscriptionsOrdinal = reader.GetOrdinal(RecipientListsSql.SubscriptionsColumn);
        int unsubscriptionsOrdinal = reader.GetOrdinal(RecipientListsSql.UnsubscriptionsColumn);
        int changeOrdinal = reader.GetOrdinal(RecipientListsSql.SubscriberChangeColumn);

        var rows = new List<RecipientListsDailyRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                DateOnly.FromDateTime(reader.GetDateTime(dateOrdinal)),
                reader.GetInt32(subscriptionsOrdinal),
                reader.GetInt32(unsubscriptionsOrdinal),
                reader.GetInt32(changeOrdinal)));
        }

        return rows;
    }
}
