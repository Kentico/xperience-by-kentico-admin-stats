using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentInventory;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class ContentInventorySqlTests
{
    // Content types, languages, statuses, age, oldest, workflow.
    private const int FilteredStatements = 6;

    [Test]
    public void Build_NoFilters_UsesNoKindOrChannelParameter()
    {
        string sql = ContentInventorySql.Build(hasKind: false, hasChannel: false);

        Assert.That(sql, Does.Contain(ContentInventorySql.ClassTypeParameter));
        Assert.That(sql, Does.Not.Contain(ContentInventorySql.KindParameter + Environment.NewLine));
        Assert.That(sql, Does.Not.Contain("= " + ContentInventorySql.KindParameter));
        Assert.That(sql, Does.Not.Contain(ContentInventorySql.ChannelParameter));
        Assert.That(sql, Does.Not.Contain("{").And.Not.Contain("}"));
    }

    [Test]
    public void Build_ReturnsEightStatements()
    {
        string sql = ContentInventorySql.Build(hasKind: true, hasChannel: true);

        Assert.That(sql.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), Has.Length.EqualTo(8));
    }

    [Test]
    public void Build_Kind_FiltersEveryItemStatement()
    {
        string sql = ContentInventorySql.Build(hasKind: true, hasChannel: false);

        Assert.That(Count(sql, "C.[ClassContentTypeType] = " + ContentInventorySql.KindParameter), Is.EqualTo(FilteredStatements));
        Assert.That(sql, Does.Not.Contain(ContentInventorySql.ChannelParameter));
    }

    [Test]
    public void Build_Channel_FiltersEveryItemStatement_AndKeepsContentTypesWithoutItems()
    {
        string sql = ContentInventorySql.Build(hasKind: false, hasChannel: true);

        Assert.That(Count(sql, "I.[ContentItemChannelID] = " + ContentInventorySql.ChannelParameter), Is.EqualTo(FilteredStatements));

        // In the content types statement the channel belongs to the LEFT JOIN (before WHERE), not the WHERE clause.
        string contentTypes = sql[..sql.IndexOf(';')];
        Assert.That(
            contentTypes.IndexOf(ContentInventorySql.ChannelParameter, StringComparison.Ordinal),
            Is.LessThan(contentTypes.IndexOf("WHERE", StringComparison.Ordinal)));
    }

    [Test]
    public void Build_UnusedStatements_AreGatedAndCheckReferences()
    {
        string sql = ContentInventorySql.Build(hasKind: false, hasChannel: false);

        Assert.That(Count(sql, ContentInventorySql.IncludeUnusedParameter + " = 1"), Is.EqualTo(2));
        Assert.That(Count(sql, "[CMS_ContentItemReference]"), Is.EqualTo(2));
        Assert.That(sql, Does.Contain("R.[ContentItemReferenceTargetItemID] = I.[ContentItemID]"));
    }

    private static int Count(string text, string value) =>
        (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;
}
