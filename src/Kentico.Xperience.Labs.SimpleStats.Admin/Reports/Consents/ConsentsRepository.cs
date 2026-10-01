using System.Data;
using System.Data.Common;

using CMS.DataEngine;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Consents;

/// <summary>
/// Reads aggregated consent agreement data from the database.
/// </summary>
internal interface IConsentsRepository
{
    /// <summary>
    /// Returns all consents with their numbers, consent events per day from <paramref name="previousFrom"/> to <paramref name="to"/>
    /// and totals. <see cref="ConsentsReportData.Unavailable"/> when the consent tables do not exist.
    /// </summary>
    /// <param name="previousFrom">First day of the previous period (inclusive).</param>
    /// <param name="from">First day of the range (inclusive).</param>
    /// <param name="to">Last day of the range (inclusive).</param>
    /// <param name="consentId">Consent filter, <c>null</c> for all consents. An ID that is not a consent means all consents.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<ConsentsReportData> GetData(DateOnly previousFrom, DateOnly from, DateOnly to, int? consentId, CancellationToken cancellationToken);
}

internal sealed class ConsentsRepository : IConsentsRepository
{
    private const string ReportName = "consents";

    public async Task<ConsentsReportData> GetData(DateOnly previousFrom, DateOnly from, DateOnly to, int? consentId, CancellationToken cancellationToken)
    {
        var parameters = new QueryDataParameters
        {
            new DataParameter(ConsentsSql.PreviousFromParameter, previousFrom.ToDateTime(TimeOnly.MinValue)),
            new DataParameter(ConsentsSql.FromParameter, from.ToDateTime(TimeOnly.MinValue)),
            new DataParameter(ConsentsSql.ToExclusiveParameter, to.AddDays(1).ToDateTime(TimeOnly.MinValue)),
            new DataParameter(ConsentsSql.ConsentIdParameter, consentId ?? 0),
        };

        await using var reader = await ConnectionHelper.ExecuteReaderAsync(
            ConsentsSql.BuildReport(),
            parameters,
            QueryTypeEnum.SQLQuery,
            CommandBehavior.Default,
            cancellationToken);

        if (!await StatsSql.IsAvailable(reader, ConsentsSql.AvailableColumn, cancellationToken))
        {
            return ConsentsReportData.Unavailable;
        }

        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var consents = await ReadConsents(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var daily = await ReadDaily(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var (agreedBefore, allAgreed, language) = await ReadTotals(reader, cancellationToken);

        return new(true, consents, daily, agreedBefore, allAgreed) { DefaultLanguageName = language };
    }

    private static async Task<IReadOnlyList<ConsentsConsentRow>> ReadConsents(DbDataReader reader, CancellationToken cancellationToken)
    {
        int idOrdinal = reader.GetOrdinal(ConsentsSql.ConsentIdColumn);
        int nameOrdinal = reader.GetOrdinal(ConsentsSql.ConsentNameColumn);
        int agreementsOrdinal = reader.GetOrdinal(ConsentsSql.AgreementsColumn);
        int previousAgreementsOrdinal = reader.GetOrdinal(ConsentsSql.PreviousAgreementsColumn);
        int revocationsOrdinal = reader.GetOrdinal(ConsentsSql.RevocationsColumn);
        int agreedOrdinal = reader.GetOrdinal(ConsentsSql.AgreedColumn);
        int previousAgreedOrdinal = reader.GetOrdinal(ConsentsSql.PreviousAgreedColumn);
        int olderOrdinal = reader.GetOrdinal(ConsentsSql.OlderTextColumn);

        var rows = new List<ConsentsConsentRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.GetInt32(idOrdinal),
                reader.IsDBNull(nameOrdinal) ? null : reader.GetString(nameOrdinal),
                reader.GetInt32(agreementsOrdinal),
                reader.GetInt32(previousAgreementsOrdinal),
                reader.GetInt32(revocationsOrdinal),
                reader.GetInt32(agreedOrdinal),
                reader.GetInt32(previousAgreedOrdinal),
                reader.GetInt32(olderOrdinal)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<ConsentsDailyRow>> ReadDaily(DbDataReader reader, CancellationToken cancellationToken)
    {
        int dateOrdinal = reader.GetOrdinal(ConsentsSql.DateColumn);
        int agreementsOrdinal = reader.GetOrdinal(ConsentsSql.AgreementsColumn);
        int revocationsOrdinal = reader.GetOrdinal(ConsentsSql.RevocationsColumn);
        int changeOrdinal = reader.GetOrdinal(ConsentsSql.AgreedChangeColumn);

        var rows = new List<ConsentsDailyRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                DateOnly.FromDateTime(reader.GetDateTime(dateOrdinal)),
                reader.GetInt32(agreementsOrdinal),
                reader.GetInt32(revocationsOrdinal),
                reader.GetInt32(changeOrdinal)));
        }

        return rows;
    }

    private static async Task<(int AgreedBefore, int AllAgreed, string? Language)> ReadTotals(DbDataReader reader, CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken))
        {
            return (0, 0, null);
        }

        int languageOrdinal = reader.GetOrdinal(ConsentsSql.DefaultLanguageColumn);

        return (
            reader.GetInt32(reader.GetOrdinal(ConsentsSql.AgreedBeforeColumn)),
            reader.GetInt32(reader.GetOrdinal(ConsentsSql.AllAgreedColumn)),
            reader.IsDBNull(languageOrdinal) ? null : reader.GetString(languageOrdinal));
    }
}
