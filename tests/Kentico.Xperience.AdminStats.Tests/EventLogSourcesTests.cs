using Kentico.Xperience.AdminStats.Reports.EventLog;

namespace Kentico.Xperience.AdminStats.Tests;

public class EventLogSourcesTests
{
    [Test]
    public void XperiencePrefixes_AreCmsAndKentico()
    {
        Assert.That(EventLogSources.XperiencePrefixes, Is.EqualTo(new[] { "CMS.", "Kentico." }));
    }

    [TestCase("CMS.ContentEngine.ContentItemAssetVariantCacheCleaner", true)]
    [TestCase("cms.eventlog", true)]
    [TestCase("Kentico.Xperience.AzureStorage.AzureStorageCacheCleaner", true)]
    [TestCase("kentico.xperience.admin", true)]
    [TestCase("Kentico.Xperience.Foo", true)]
    [TestCase("Kentico.Other", true)]
    [TestCase("Kentico.Web.Mvc.KenticoErrorHandlingMiddleware", true)]
    [TestCase("KenticoFoo", false)]
    [TestCase("MyCompany.Kentico", false)]
    [TestCase("Microsoft.AspNetCore.Diagnostics.DeveloperExceptionPageMiddleware", false)]
    [TestCase("CMSCustom.Import", false)]
    [TestCase("Acme.Integrations.Crm", false)]
    [TestCase("WebFarmMonitor", true)]
    [TestCase("webfarmmonitor", true)]
    [TestCase("WebFarmMonitorX", false)]
    [TestCase("My.WebFarmMonitor", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    public void IsXperience_MatchesPrefixesAndExactNamesCaseInsensitively(string? source, bool expected)
    {
        Assert.That(EventLogSources.IsXperience(source), Is.EqualTo(expected));
    }

    [Test]
    public void XperienceNames_AreExactNames()
    {
        Assert.That(EventLogSources.XperienceNames, Is.EqualTo(new[] { "WebFarmMonitor" }));
    }

    [TestCase("CMS.", "CMS.%")]
    [TestCase("Kentico.", "Kentico.%")]
    [TestCase(@"a_b%c[d]\e", @"a\_b\%c\[d]\\e%")]
    [TestCase("", "%")]
    public void ToLikePattern_EscapesWildcards(string prefix, string expected)
    {
        Assert.That(EventLogSources.ToLikePattern(prefix), Is.EqualTo(expected));
    }
}
