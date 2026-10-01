using System.Text;
using System.Text.RegularExpressions;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.FormSubmissions;

/// <summary>
/// Builds the one-batch aggregate over all form data tables.
/// Table names are identifiers (they cannot be parameters): they come only from form class metadata,
/// must match <c>^[A-Za-z0-9_]{1,128}$</c> and are wrapped in brackets. All values are parameters.
/// </summary>
internal static partial class FormSubmissionsSql
{
    public const string FromParameter = "@From";
    public const string ToExclusiveParameter = "@ToExclusive";
    public const string FormIdColumn = "FormID";
    public const string DateColumn = "SubmissionDate";
    public const string CountColumn = "SubmissionCount";

    private const string UnionAll = " UNION ALL ";

    /// <summary>
    /// Form data table to count, with the name of the parameter that carries its form ID.
    /// </summary>
    public sealed record Table(int FormId, string QuotedName, string FormIdParameter);

    /// <summary>
    /// SQL batch and the tables it reads.
    /// </summary>
    public sealed record Command(string Sql, IReadOnlyList<Table> Tables);

    [GeneratedRegex("^[A-Za-z0-9_]{1,128}$", RegexOptions.CultureInvariant)]
    private static partial Regex TableNameRegex();

    /// <summary>
    /// Returns the table name in brackets, or <c>null</c> when it is empty or has characters other than letters, digits and underscores.
    /// </summary>
    public static string? QuoteTableName(string? tableName) =>
        tableName is not null && TableNameRegex().IsMatch(tableName) ? $"[{tableName}]" : null;

    /// <summary>
    /// Builds the batch. Forms with an invalid table name are skipped.
    /// Returns <c>null</c> when no form has a valid table name.
    /// </summary>
    /// <remarks>
    /// The batch checks each table at run time (<c>OBJECT_ID</c> and <c>COL_LENGTH</c> for <c>FormInserted</c>),
    /// adds one <c>SELECT ... GROUP BY</c> per existing table to a <c>UNION ALL</c> statement and runs it with
    /// <c>sp_executesql</c>, passing the same parameters. Missing tables are skipped instead of failing the batch.
    /// It always returns one result set (<see cref="FormIdColumn"/>, <see cref="DateColumn"/>, <see cref="CountColumn"/>).
    /// </remarks>
    public static Command? Build(IEnumerable<(int FormId, string? TableName)> forms)
    {
        var tables = new List<Table>();
        foreach (var (formId, tableName) in forms)
        {
            if (QuoteTableName(tableName) is string quoted)
            {
                tables.Add(new(formId, quoted, $"@FormID{tables.Count}"));
            }
        }

        if (tables.Count == 0)
        {
            return null;
        }

        var sql = new StringBuilder();
        sql.AppendLine("SET NOCOUNT ON;");
        sql.AppendLine("DECLARE @Sql nvarchar(max) = N'';");

        foreach (var table in tables)
        {
            // The quoted name has no quotes (checked by the regex), so it is safe inside N'...'.
            sql.Append("IF OBJECT_ID(N'").Append(table.QuotedName).Append("', N'U') IS NOT NULL")
                .Append(" AND COL_LENGTH(N'").Append(table.QuotedName).AppendLine("', N'FormInserted') IS NOT NULL");
            sql.Append("    SET @Sql += N'").Append(UnionAll)
                .Append("SELECT ").Append(table.FormIdParameter).Append(" AS [").Append(FormIdColumn).Append("], ")
                .Append("CAST([FormInserted] AS date) AS [").Append(DateColumn).Append("], ")
                .Append("COUNT(*) AS [").Append(CountColumn).Append("] ")
                .Append("FROM ").Append(table.QuotedName).Append(' ')
                .Append("WHERE [FormInserted] >= ").Append(FromParameter)
                .Append(" AND [FormInserted] < ").Append(ToExclusiveParameter).Append(' ')
                .AppendLine("GROUP BY CAST([FormInserted] AS date)';");
        }

        string parameterDeclarations = string.Join(", ",
            new[] { $"{FromParameter} datetime2", $"{ToExclusiveParameter} datetime2" }
                .Concat(tables.Select(t => $"{t.FormIdParameter} int")));

        string parameterValues = string.Join(", ",
            new[] { FromParameter, ToExclusiveParameter }
                .Concat(tables.Select(t => t.FormIdParameter))
                .Select(p => $"{p} = {p}"));

        sql.AppendLine("IF @Sql = N''");
        sql.Append("    SELECT CAST(NULL AS int) AS [").Append(FormIdColumn).Append("], ")
            .Append("CAST(NULL AS date) AS [").Append(DateColumn).Append("], ")
            .Append("0 AS [").Append(CountColumn).AppendLine("] WHERE 1 = 0;");
        sql.AppendLine("ELSE");
        sql.AppendLine("BEGIN");
        sql.Append("    SET @Sql = STUFF(@Sql, 1, ").Append(UnionAll.Length).AppendLine(", N'');");
        sql.Append("    EXEC sys.sp_executesql @Sql, N'").Append(parameterDeclarations).Append("', ")
            .Append(parameterValues).AppendLine(";");
        sql.AppendLine("END");

        return new(sql.ToString(), tables);
    }
}
