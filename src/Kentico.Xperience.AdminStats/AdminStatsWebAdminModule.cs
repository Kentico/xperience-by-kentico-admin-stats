using CMS.Core;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.AdminStats.Reports.ActivityCounts;
using Kentico.Xperience.AdminStats.Reports.Consents;
using Kentico.Xperience.AdminStats.Reports.ContentInventory;
using Kentico.Xperience.AdminStats.Reports.Customers;
using Kentico.Xperience.AdminStats.Reports.EventLog;
using Kentico.Xperience.AdminStats.Reports.FormSubmissions;
using Kentico.Xperience.AdminStats.Reports.Members;
using Kentico.Xperience.AdminStats.Reports.NewContacts;
using Kentico.Xperience.AdminStats.Reports.OrdersRevenue;
using Kentico.Xperience.AdminStats.Reports.TopPages;
using Kentico.Xperience.AdminStats.Shared;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

[assembly: CMS.RegisterModule(typeof(Kentico.Xperience.AdminStats.AdminStatsWebAdminModule))]

namespace Kentico.Xperience.AdminStats;

internal sealed class AdminStatsWebAdminModule : AdminModule
{
    public AdminStatsWebAdminModule()
        : base("Kentico.Xperience.AdminStats.Admin")
    {
    }

    protected override void OnPreInit(ModulePreInitParameters parameters)
    {
        base.OnPreInit(parameters);

        RegisterServices(parameters.Services);
    }

    protected override void OnInit()
    {
        base.OnInit();

        RegisterClientModule("kentico", "xperience-admin-stats");
    }

    internal static void RegisterServices(IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddTransient<IStatsChannelOptionsProvider, StatsChannelOptionsProvider>();
        services.TryAddTransient<IActivityCountsRepository, ActivityCountsRepository>();
        services.TryAddTransient<IStatsCacheInvalidator, StatsCacheInvalidator>();
        services.TryAddTransient<IActivityCountsService, ActivityCountsService>();
        services.TryAddTransient<ITopPagesRepository, TopPagesRepository>();
        services.TryAddTransient<ITopPagesService, TopPagesService>();
        services.TryAddTransient<INewContactsRepository, NewContactsRepository>();
        services.TryAddTransient<INewContactsService, NewContactsService>();
        services.TryAddTransient<IStatsAdminLinks, StatsAdminLinks>();
        services.TryAddTransient<IFormSubmissionsRepository, FormSubmissionsRepository>();
        services.TryAddTransient<IFormSubmissionsService, FormSubmissionsService>();
        services.TryAddTransient<IContentInventoryRepository, ContentInventoryRepository>();
        services.TryAddTransient<IContentInventoryService, ContentInventoryService>();
        services.TryAddTransient<IEventLogRepository, EventLogRepository>();
        services.TryAddTransient<IEventLogReportService, EventLogReportService>();
        services.TryAddTransient<IStatsAmountFormatter, StatsAmountFormatter>();
        services.TryAddTransient<IOrdersRevenueRepository, OrdersRevenueRepository>();
        services.TryAddTransient<IOrdersRevenueService, OrdersRevenueService>();
        services.TryAddTransient<ICustomersRepository, CustomersRepository>();
        services.TryAddTransient<ICustomersService, CustomersService>();
        services.TryAddTransient<IMembersRepository, MembersRepository>();
        services.TryAddTransient<IMembersService, MembersService>();
        services.TryAddTransient<IConsentsRepository, ConsentsRepository>();
        services.TryAddTransient<IConsentsService, ConsentsService>();
    }
}
