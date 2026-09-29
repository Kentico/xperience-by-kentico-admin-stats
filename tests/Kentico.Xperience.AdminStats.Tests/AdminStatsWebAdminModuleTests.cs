using CMS;

namespace Kentico.Xperience.AdminStats.Tests;

public class AdminStatsWebAdminModuleTests
{
    [Test]
    public void Assembly_RegistersAdminModule()
    {
        var registered = typeof(AdminStatsWebAdminModule).Assembly
            .GetCustomAttributes(typeof(RegisterModuleAttribute), false)
            .Cast<RegisterModuleAttribute>();

        Assert.That(registered.Select(a => a.Type), Does.Contain(typeof(AdminStatsWebAdminModule)));
    }
}
