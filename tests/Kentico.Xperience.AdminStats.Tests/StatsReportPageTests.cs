using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.AdminStats.Admin;

namespace Kentico.Xperience.AdminStats.Tests;

public class StatsReportPageTests
{
    [Test]
    public void Application_DeclaresExportPermission()
    {
        var permissions = typeof(StatsApplicationPage)
            .GetCustomAttributes(typeof(UIPermissionAttribute), false)
            .Cast<UIPermissionAttribute>();

        Assert.That(
            permissions.Select(p => (p.Name, p.DisplayName)),
            Does.Contain(("Kentico.Xperience.AdminStats.Export", "Export")));
    }

    [Test]
    public void EveryReportPage_IsStatsReportPage()
    {
        var reportPages = StatsNavigation.GetChildPages(typeof(StatsApplicationPage))
            .Where(section => typeof(StatsSectionPage).IsAssignableFrom(section.Type))
            .SelectMany(section => StatsNavigation.GetChildPages(section.Type))
            .Select(page => page.Type)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(reportPages, Has.Count.EqualTo(10));
            Assert.That(reportPages.Where(type => !IsStatsReportPage(type)), Is.Empty);
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task ConfigureTemplateProperties_SetsCanExportFromExportPermission(bool granted)
    {
        var evaluator = new FakePermissionEvaluator(granted);
        var page = new TestReportPage(evaluator);

        var properties = await page.ConfigureTemplateProperties(new TestClientProperties());

        Assert.Multiple(() =>
        {
            Assert.That(properties.CanExport, Is.EqualTo(granted));
            Assert.That(properties.ReportConfigured, Is.True);
            Assert.That(evaluator.Evaluated, Is.EqualTo(new[] { StatsPermissions.EXPORT }));
        });
    }

    private static bool IsStatsReportPage(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(StatsReportPage<>))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class FakePermissionEvaluator(bool granted) : IUIPermissionEvaluator
    {
        public List<string> Evaluated { get; } = [];

        public Task<UIPermissionEvaluationResult> Evaluate(string permission)
        {
            Evaluated.Add(permission);

            return Task.FromResult(new UIPermissionEvaluationResult(granted));
        }
    }

    private sealed class TestClientProperties : StatsReportClientProperties
    {
        public bool ReportConfigured { get; set; }
    }

    private sealed class TestReportPage(IUIPermissionEvaluator permissionEvaluator)
        : StatsReportPage<TestClientProperties>(permissionEvaluator)
    {
        protected override Task<TestClientProperties> ConfigureReportProperties(TestClientProperties properties)
        {
            properties.ReportConfigured = true;

            return Task.FromResult(properties);
        }
    }
}
