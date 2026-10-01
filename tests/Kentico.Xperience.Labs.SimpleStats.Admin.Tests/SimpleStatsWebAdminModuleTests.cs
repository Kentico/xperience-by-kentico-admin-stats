using CMS;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class SimpleStatsWebAdminModuleTests
{
    [Test]
    public void Assembly_RegistersAdminModule()
    {
        var registered = typeof(SimpleStatsWebAdminModule).Assembly
            .GetCustomAttributes(typeof(RegisterModuleAttribute), false)
            .Cast<RegisterModuleAttribute>();

        Assert.That(registered.Select(a => a.Type), Does.Contain(typeof(SimpleStatsWebAdminModule)));
    }
}
