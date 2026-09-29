using Kentico.Xperience.Admin.Base;

[assembly: CMS.AssemblyDiscoverable]
[assembly: CMS.RegisterModule(typeof(Kentico.Xperience.AdminStats.AdminStatsWebAdminModule))]

namespace Kentico.Xperience.AdminStats;

internal sealed class AdminStatsWebAdminModule : AdminModule
{
    public AdminStatsWebAdminModule()
        : base("Kentico.Xperience.AdminStats.Admin")
    {
    }

    protected override void OnInit()
    {
        base.OnInit();

        RegisterClientModule("kentico", "xperience-admin-stats");
    }
}
