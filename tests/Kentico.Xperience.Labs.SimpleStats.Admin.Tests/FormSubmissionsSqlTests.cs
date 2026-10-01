using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.FormSubmissions;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class FormSubmissionsSqlTests
{
    [TestCase("Form_DancingGoat_CoffeeSampleList", "[Form_DancingGoat_CoffeeSampleList]")]
    [TestCase("Form_2023_09_12", "[Form_2023_09_12]")]
    public void QuoteTableName_BracketsValidNames(string name, string expected) =>
        Assert.That(FormSubmissionsSql.QuoteTableName(name), Is.EqualTo(expected));

    [TestCase(null)]
    [TestCase("")]
    [TestCase("Form X")]
    [TestCase("dbo.Form_X")]
    [TestCase("Form]; DROP TABLE CMS_User; --")]
    [TestCase("Form'X")]
    [TestCase("[Form_X]")]
    [TestCase("Formé")]
    public void QuoteTableName_RejectsInvalidNames(string? name) =>
        Assert.That(FormSubmissionsSql.QuoteTableName(name), Is.Null);

    [Test]
    public void QuoteTableName_RejectsNamesLongerThanSqlIdentifiers()
    {
        Assert.That(FormSubmissionsSql.QuoteTableName(new string('a', 128)), Is.Not.Null);
        Assert.That(FormSubmissionsSql.QuoteTableName(new string('a', 129)), Is.Null);
    }

    [Test]
    public void Build_NoValidTables_ReturnsNull()
    {
        Assert.That(FormSubmissionsSql.Build([]), Is.Null);
        Assert.That(FormSubmissionsSql.Build([(1, null), (2, "bad name")]), Is.Null);
    }

    [Test]
    public void Build_SkipsInvalidNames_AndNumbersParametersPerTable()
    {
        var command = FormSubmissionsSql.Build([(7, "Form_A"), (8, "bad;name"), (9, "Form_B")]);

        Assert.That(command, Is.Not.Null);
        Assert.That(command!.Tables.Select(t => (t.FormId, t.QuotedName, t.FormIdParameter)), Is.EqualTo(new[]
        {
            (7, "[Form_A]", "@FormID0"),
            (9, "[Form_B]", "@FormID1"),
        }));
        Assert.That(command.Sql, Does.Not.Contain("bad"));
    }

    [Test]
    public void Build_ChecksTableAndColumn_AndPassesValuesAsParameters()
    {
        var command = FormSubmissionsSql.Build([(7, "Form_A"), (9, "Form_B")])!;

        Assert.Multiple(() =>
        {
            Assert.That(command.Sql, Does.Contain("OBJECT_ID(N'[Form_A]', N'U') IS NOT NULL"));
            Assert.That(command.Sql, Does.Contain("COL_LENGTH(N'[Form_B]', N'FormInserted') IS NOT NULL"));
            Assert.That(command.Sql, Does.Contain("FROM [Form_A] WHERE [FormInserted] >= @From AND [FormInserted] < @ToExclusive"));
            Assert.That(command.Sql, Does.Contain("SELECT @FormID1 AS [FormID]"));
            Assert.That(command.Sql, Does.Contain("UNION ALL"));
            Assert.That(command.Sql, Does.Contain("N'@From datetime2, @ToExclusive datetime2, @FormID0 int, @FormID1 int'"));
            Assert.That(command.Sql, Does.Contain("@From = @From, @ToExclusive = @ToExclusive, @FormID0 = @FormID0, @FormID1 = @FormID1"));
            // Form IDs are parameters, never inlined.
            Assert.That(command.Sql, Does.Not.Contain(" 7 "));
            Assert.That(command.Sql, Does.Not.Contain(" 9 "));
        });
    }
}
