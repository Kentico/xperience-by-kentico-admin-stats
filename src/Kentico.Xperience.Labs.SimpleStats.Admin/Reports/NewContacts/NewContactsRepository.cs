using System.Data;

using CMS.DataEngine;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.NewContacts;

/// <summary>
/// Reads aggregated contact counts from the database.
/// </summary>
internal interface INewContactsRepository
{
    /// <summary>
    /// Returns created contacts per day, split into identified (has an email address) and anonymous.
    /// </summary>
    public Task<IReadOnlyList<NewContactsDailyCount>> GetDailyCounts(DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

internal sealed class NewContactsRepository : INewContactsRepository
{
    // Constant SQL; all values are passed as parameters.
    // ContactCreated is datetime2 NOT NULL. Identified = ContactEmail is not NULL or empty.
    // SQL Server ignores trailing spaces in "<>" comparisons, so an email of only spaces counts as empty.
    // Merged contacts are deleted, so they are not counted.
    private const string Query = """
        SELECT
            CAST(C.[ContactCreated] AS date) AS [ContactDate],
            SUM(CASE WHEN C.[ContactEmail] <> N'' THEN 1 ELSE 0 END) AS [IdentifiedCount],
            SUM(CASE WHEN C.[ContactEmail] <> N'' THEN 0 ELSE 1 END) AS [AnonymousCount]
        FROM [OM_Contact] C
        WHERE C.[ContactCreated] >= @From
            AND C.[ContactCreated] < @ToExclusive
        GROUP BY CAST(C.[ContactCreated] AS date)
        """;

    public async Task<IReadOnlyList<NewContactsDailyCount>> GetDailyCounts(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var parameters = new QueryDataParameters
        {
            new DataParameter("@From", from.ToDateTime(TimeOnly.MinValue)),
            new DataParameter("@ToExclusive", to.AddDays(1).ToDateTime(TimeOnly.MinValue)),
        };

        await using var reader = await ConnectionHelper.ExecuteReaderAsync(Query, parameters, QueryTypeEnum.SQLQuery, CommandBehavior.Default, cancellationToken);

        var rows = new List<NewContactsDailyCount>();
        int dateOrdinal = reader.GetOrdinal("ContactDate");
        int identifiedOrdinal = reader.GetOrdinal("IdentifiedCount");
        int anonymousOrdinal = reader.GetOrdinal("AnonymousCount");

        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                DateOnly.FromDateTime(reader.GetDateTime(dateOrdinal)),
                reader.GetInt32(identifiedOrdinal),
                reader.GetInt32(anonymousOrdinal)));
        }

        return rows;
    }
}
