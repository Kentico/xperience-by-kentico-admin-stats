using System.Data;

using CMS.DataEngine;
using CMS.OnlineForms;

namespace Kentico.Xperience.AdminStats.Reports.FormSubmissions;

/// <summary>
/// Reads forms and aggregated submission counts from the database.
/// </summary>
internal interface IFormSubmissionsRepository
{
    /// <summary>
    /// Returns all forms and their submissions per day in the range.
    /// </summary>
    public Task<FormSubmissionsData> GetData(DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

internal sealed class FormSubmissionsRepository(IInfoProvider<BizFormInfo> formProvider) : IFormSubmissionsRepository
{
    private readonly IInfoProvider<BizFormInfo> formProvider = formProvider;

    public async Task<FormSubmissionsData> GetData(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var forms = await GetForms(cancellationToken);

        // One batch for all form tables. Fine for tens of forms; each form adds one UNION ALL branch and one parameter.
        var command = FormSubmissionsSql.Build(forms.Select(f => (f.FormId, f.TableName)));
        if (command is null)
        {
            return new(forms, []);
        }

        var parameters = new QueryDataParameters
        {
            new DataParameter(FormSubmissionsSql.FromParameter, from.ToDateTime(TimeOnly.MinValue)),
            new DataParameter(FormSubmissionsSql.ToExclusiveParameter, to.AddDays(1).ToDateTime(TimeOnly.MinValue)),
        };
        foreach (var table in command.Tables)
        {
            parameters.Add(new DataParameter(table.FormIdParameter, table.FormId));
        }

        await using var reader = await ConnectionHelper.ExecuteReaderAsync(command.Sql, parameters, QueryTypeEnum.SQLQuery, CommandBehavior.Default, cancellationToken);

        var rows = new List<FormDailyCount>();
        int formOrdinal = reader.GetOrdinal(FormSubmissionsSql.FormIdColumn);
        int dateOrdinal = reader.GetOrdinal(FormSubmissionsSql.DateColumn);
        int countOrdinal = reader.GetOrdinal(FormSubmissionsSql.CountColumn);

        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.GetInt32(formOrdinal),
                DateOnly.FromDateTime(reader.GetDateTime(dateOrdinal)),
                reader.GetInt32(countOrdinal)));
        }

        return new(forms, rows);
    }

    private async Task<IReadOnlyList<FormDefinition>> GetForms(CancellationToken cancellationToken)
    {
        var forms = await formProvider
            .Get()
            .Columns(
                nameof(BizFormInfo.FormID),
                nameof(BizFormInfo.FormName),
                nameof(BizFormInfo.FormDisplayName),
                nameof(BizFormInfo.FormClassID))
            .GetEnumerableTypedResultAsync(cancellationToken: cancellationToken);

        // Data classes are cached by the provider, so this does not add database queries per form.
        return forms
            .Select(form => new FormDefinition(
                form.FormID,
                form.FormName,
                string.IsNullOrWhiteSpace(form.FormDisplayName) ? form.FormName : form.FormDisplayName,
                DataClassInfoProvider.GetDataClassInfo(form.FormClassID)?.ClassTableName))
            .ToList();
    }
}
