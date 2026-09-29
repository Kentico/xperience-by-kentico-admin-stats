using CMS.Membership;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.AdminStats.Admin;
using Kentico.Xperience.AdminStats.Reports.ActivityCounts;
using Kentico.Xperience.AdminStats.Shared;

[assembly: UIPage(
    uiPageType: typeof(ActivityCountsPage),
    parentType: typeof(StatsApplicationPage),
    slug: "activity-counts",
    name: "Activity counts",
    templateName: ActivityCountsPage.TEMPLATE_NAME,
    order: 100,
    Icon = Icons.Graph)]

namespace Kentico.Xperience.AdminStats.Admin;

/// <summary>
/// Activity counts by type over time.
/// </summary>
[UIEvaluatePermission(SystemPermissions.VIEW)]
public sealed class ActivityCountsPage(
    IActivityCountsService activityCountsService,
    IStatsChannelOptionsProvider channelOptionsProvider,
    TimeProvider clock) : Page<ActivityCountsClientProperties>
{
    public const string TEMPLATE_NAME = "@kentico/xperience-admin-stats/ActivityCounts";

    private readonly IActivityCountsService activityCountsService = activityCountsService;
    private readonly IStatsChannelOptionsProvider channelOptionsProvider = channelOptionsProvider;
    private readonly TimeProvider clock = clock;

    public override async Task<ActivityCountsClientProperties> ConfigureTemplateProperties(ActivityCountsClientProperties properties)
    {
        var query = new StatsFilter().Normalize(GetToday());

        properties.Report = await activityCountsService.GetReport(query, refresh: false, CancellationToken.None);
        properties.Channels = await channelOptionsProvider.GetChannelOptions(CancellationToken.None);
        properties.Today = GetToday();

        return properties;
    }

    [PageCommand(CommandName = "LOAD", Permission = SystemPermissions.VIEW)]
    public async Task<ICommandResponse<ActivityCountsResult>> Load(StatsLoadRequest request, CancellationToken cancellationToken)
    {
        // Refresh re-runs one aggregate query per click. The command requires the View permission.
        var query = (request?.Filter ?? new StatsFilter()).Normalize(GetToday());
        var report = await activityCountsService.GetReport(query, request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(report);
    }

    // ActivityCreated values are compared as stored (no time zone conversion), so "today" uses the server time zone.
    private DateOnly GetToday() => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
}

public sealed class ActivityCountsClientProperties : TemplateClientProperties
{
    public ActivityCountsResult? Report { get; set; }

    public IReadOnlyList<StatsChannelOption> Channels { get; set; } = [];

    /// <summary>
    /// Server date used for date presets.
    /// </summary>
    public DateOnly Today { get; set; }
}
