using System.Data;

using CMS.DataEngine;

namespace Kentico.Xperience.AdminStats.Reports.Commerce;

/// <summary>
/// Order status shown in a status filter, in the project's status order (<c>OrderStatusOrder</c>).
/// </summary>
public sealed record CommerceOrderStatusOption(int Id, string DisplayName);

/// <summary>
/// Order statuses for the status filters of the commerce reports. Statuses are editable per project,
/// so filters are checked against the current statuses (never hardcoded names or IDs).
/// </summary>
internal static class CommerceOrderStatuses
{
    /// <summary>
    /// Cache name part of the statuses. Shared by the commerce reports (the name of the first report that used it is kept,
    /// so its cache key does not change).
    /// </summary>
    public const string CacheName = "orders-revenue-statuses";

    /// <summary>
    /// Reads the order statuses in status order. Empty when the commerce tables do not exist.
    /// </summary>
    public static async Task<IReadOnlyList<CommerceOrderStatusOption>> Read(CancellationToken cancellationToken)
    {
        await using var reader = await ConnectionHelper.ExecuteReaderAsync(
            CommerceSql.BuildStatuses(),
            new QueryDataParameters(),
            QueryTypeEnum.SQLQuery,
            CommandBehavior.Default,
            cancellationToken);

        if (!await CommerceSql.IsAvailable(reader, cancellationToken))
        {
            return [];
        }

        await CommerceSql.NextResult(reader, "order statuses", cancellationToken);

        int idOrdinal = reader.GetOrdinal(CommerceSql.StatusIdColumn);
        int nameOrdinal = reader.GetOrdinal(CommerceSql.StatusNameColumn);

        var rows = new List<CommerceOrderStatusOption>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(reader.GetInt32(idOrdinal), reader.GetString(nameOrdinal)));
        }

        return rows;
    }

    /// <summary>
    /// Returns <paramref name="orderStatusId"/> when it is one of <paramref name="statuses"/>, else <c>null</c> (all statuses).
    /// </summary>
    public static int? Resolve(IReadOnlyList<CommerceOrderStatusOption> statuses, int? orderStatusId) =>
        orderStatusId is int id && statuses.Any(s => s.Id == id) ? id : null;
}
