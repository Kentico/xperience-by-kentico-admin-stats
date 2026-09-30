using Kentico.Xperience.AdminStats.Reports.FormSubmissions;
using Kentico.Xperience.AdminStats.Shared;

namespace Kentico.Xperience.AdminStats.Tests;

public class FormSubmissionsReportBuilderTests
{
    private static readonly StatsQuery query = new(new(2026, 9, 1), new(2026, 9, 3), StatsGrouping.Day, null);

    private static readonly FormDefinition[] forms =
    [
        new(1, "Coffee", "Coffee sample list", "Form_Coffee"),
        new(2, "Contact", "Contact us", "Form_Contact"),
        new(3, "Subscription", "Subscription", "Form_Subscription"),
    ];

    [Test]
    public void Build_NoForms_ReturnsEmptyReport()
    {
        var result = FormSubmissionsReportBuilder.Build(query, FormSubmissionsData.Empty, _ => null);

        Assert.That(result.Total, Is.Zero);
        Assert.That(result.FormCount, Is.Zero);
        Assert.That(result.FormsWithSubmissions, Is.Zero);
        Assert.That(result.Trend.Series, Is.Empty);
        Assert.That(result.Trend.Periods, Has.Count.EqualTo(3));
        Assert.That(result.Forms.Items, Is.Empty);
    }

    [Test]
    public void Build_ListsEveryForm_IncludingFormsWithoutSubmissionsLast()
    {
        FormDailyCount[] rows =
        [
            new(3, new(2026, 9, 1), 4),
            new(3, new(2026, 9, 2), 2),
            new(1, new(2026, 9, 2), 1),
        ];

        var result = FormSubmissionsReportBuilder.Build(query, new(forms, rows), id => $"/forms/{id}/submissions");

        Assert.That(result.Forms.Items.Select(i => (i.Label, i.Value)), Is.EqualTo(new[]
        {
            ("Subscription", 6),
            ("Coffee sample list", 1),
            ("Contact us", 0),
        }));
        Assert.That(result.Forms.Items.Select(i => i.AdminPath), Is.EqualTo(new[]
        {
            "/forms/3/submissions",
            "/forms/1/submissions",
            "/forms/2/submissions",
        }));
        Assert.That(result.Forms.Items.All(i => i.Url is null));
        Assert.That(result.Forms.Total, Is.EqualTo(7));
        Assert.That(result.Forms.ItemCount, Is.EqualTo(3));
        Assert.That(result.Total, Is.EqualTo(7));
        Assert.That(result.FormCount, Is.EqualTo(3));
        Assert.That(result.FormsWithSubmissions, Is.EqualTo(2));
    }

    [Test]
    public void Build_TrendHasSeriesPerFormWithData_KeyedByCodeName()
    {
        FormDailyCount[] rows =
        [
            new(1, new(2026, 9, 1), 1),
            new(3, new(2026, 9, 3), 5),
        ];

        var result = FormSubmissionsReportBuilder.Build(query, new(forms, rows), _ => null);

        Assert.That(result.Trend.Series.Select(s => (s.Key, s.DisplayName)), Is.EqualTo(new[]
        {
            ("Subscription", "Subscription"),
            ("Coffee", "Coffee sample list"),
        }));
        Assert.That(result.Trend.Series[0].Values, Is.EqualTo(new[] { 0, 0, 5 }));
        Assert.That(result.Trend.ChannelId, Is.Null);
    }

    [Test]
    public void Build_FoldsFormsBeyondTopFiveIntoOther()
    {
        var manyForms = Enumerable.Range(1, 7)
            .Select(i => new FormDefinition(i, $"F{i}", $"Form {i}", $"Form_F{i}"))
            .ToArray();
        var rows = manyForms.Select(f => new FormDailyCount(f.FormId, new(2026, 9, 1), 10 - f.FormId)).ToArray();

        var result = FormSubmissionsReportBuilder.Build(query, new(manyForms, rows), _ => null);

        Assert.That(result.Trend.Series, Has.Count.EqualTo(FormSubmissionsReportBuilder.MaxTrendSeries + 1));
        Assert.That(result.Trend.Series[^1].Key, Is.EqualTo(StatsTimeSeriesBuilder.OtherSeries.Key));
        Assert.That(result.Trend.Series[^1].Total, Is.EqualTo(4 + 3));
        Assert.That(result.Forms.Items, Has.Count.EqualTo(7));
    }

    [Test]
    public void Build_SplitsRowsIntoPreviousPeriodAndRange()
    {
        // Range Sep 1–3 -> previous period Aug 29–31.
        FormDailyCount[] rows =
        [
            new(1, new(2026, 8, 28), 100),
            new(1, new(2026, 8, 29), 3),
            new(2, new(2026, 8, 31), 1),
            new(1, new(2026, 9, 1), 2),
            new(3, new(2026, 9, 3), 3),
        ];

        var result = FormSubmissionsReportBuilder.Build(query, new(forms, rows), _ => null);

        Assert.That(result.Total, Is.EqualTo(5));
        Assert.That(result.Trend.Series.Sum(s => s.Total), Is.EqualTo(5));
        Assert.That(result.Forms.Items.Sum(i => i.Value), Is.EqualTo(5));
        Assert.That(result.FormsWithSubmissions, Is.EqualTo(2));
        Assert.That(result.TotalComparison, Is.EqualTo(new StatsComparison(5, 4, 0.25, new(2026, 8, 29), new(2026, 8, 31))));
    }

    [Test]
    public void Build_NoPreviousSubmissions_ChangeIsNull()
    {
        FormDailyCount[] rows = [new(1, new(2026, 9, 1), 2)];

        var result = FormSubmissionsReportBuilder.Build(query, new(forms, rows), _ => null);

        Assert.That(result.TotalComparison.Previous, Is.Zero);
        Assert.That(result.TotalComparison.Change, Is.Null);
    }

    [Test]
    public void Build_IgnoresRowsOfUnknownFormsAndOutsideRange()
    {
        FormDailyCount[] rows =
        [
            new(99, new(2026, 9, 1), 50),
            new(1, new(2026, 8, 31), 50),
            new(1, new(2026, 9, 1), 2),
        ];

        var result = FormSubmissionsReportBuilder.Build(query, new(forms, rows), _ => null);

        Assert.That(result.Total, Is.EqualTo(2));
        Assert.That(result.Forms.Items[0].Value, Is.EqualTo(2));
        Assert.That(result.FormsWithSubmissions, Is.EqualTo(1));
    }
}
