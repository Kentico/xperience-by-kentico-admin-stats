using System.Data.Common;

namespace Kentico.Xperience.AdminStats.Reports.Commerce;

/// <summary>
/// SQL pieces shared by the digital commerce reports (orders and revenue, customers).
/// Only constant SQL fragments are combined; all values are parameters.
/// </summary>
internal static class CommerceSql
{
    /// <summary>Order status ID of the status filter.</summary>
    public const string OrderStatusIdParameter = "@OrderStatusId";

    public const string AvailableColumn = "CommerceAvailable";
    public const string StatusIdColumn = "OrderStatusID";
    public const string StatusNameColumn = "OrderStatusDisplayName";

    /// <summary>
    /// Status filter condition on orders aliased <c>O</c>. Appended to a <c>WHERE</c> clause.
    /// </summary>
    public const string StatusCondition = """

            AND O.[OrderOrderStatusID] = @OrderStatusId
        """;

    private const string StatusesQuery = """
        SELECT S.[OrderStatusID], S.[OrderStatusDisplayName]
        FROM [Commerce_OrderStatus] S
        ORDER BY S.[OrderStatusOrder], S.[OrderStatusID];
        """;

    /// <summary>
    /// Returns a check that selects one row (<see cref="AvailableColumn"/>): <c>0</c> and <c>RETURN</c> when one of the tables
    /// does not exist (commerce not installed or unused), else <c>1</c>. Commerce may be unlicensed, so reports must not fail without it.
    /// </summary>
    /// <param name="tables">Table names (constants of the report, never user input).</param>
    public static string BuildAvailabilityCheck(params string[] tables)
    {
        string conditions = string.Join(
            Environment.NewLine + "    OR ",
            tables.Select(table => $"OBJECT_ID(N'[{table}]', N'U') IS NULL"));

        return $"""
            IF {conditions}
            BEGIN
                SELECT CAST(0 AS bit) AS [{AvailableColumn}];
                RETURN;
            END;
            SELECT CAST(1 AS bit) AS [{AvailableColumn}];
            """;
    }

    /// <summary>
    /// Builds the batch that reads the order statuses (for status filters), in status order (<c>OrderStatusOrder</c>).
    /// Columns of <c>CMS.Commerce.OrderStatusInfo</c> (<c>Commerce_OrderStatus</c>).
    /// </summary>
    public static string BuildStatuses() =>
        string.Join(Environment.NewLine, "SET NOCOUNT ON;", BuildAvailabilityCheck("Commerce_OrderStatus"), StatusesQuery);

    /// <summary>
    /// Reads the first result set of a batch that starts with <see cref="BuildAvailabilityCheck"/>.
    /// </summary>
    public static async Task<bool> IsAvailable(DbDataReader reader, CancellationToken cancellationToken) =>
        await reader.ReadAsync(cancellationToken) && reader.GetBoolean(reader.GetOrdinal(AvailableColumn));

    /// <summary>
    /// Moves to the next result set, or throws when the batch returned fewer result sets than expected.
    /// </summary>
    /// <param name="reader">Reader of the batch.</param>
    /// <param name="reportName">Report name for the error message, for example "orders and revenue".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task NextResult(DbDataReader reader, string reportName, CancellationToken cancellationToken)
    {
        if (!await reader.NextResultAsync(cancellationToken))
        {
            throw new InvalidOperationException($"The {reportName} query returned fewer result sets than expected.");
        }
    }
}
