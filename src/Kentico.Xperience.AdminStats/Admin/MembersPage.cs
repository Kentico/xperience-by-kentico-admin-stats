using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.AdminStats.Admin;
using Kentico.Xperience.AdminStats.Reports.Members;
using Kentico.Xperience.AdminStats.Shared;

[assembly: UIPage(
    uiPageType: typeof(MembersPage),
    parentType: typeof(StatsContactsSection),
    slug: "members",
    name: "Member registrations",
    templateName: MembersPage.TEMPLATE_NAME,
    order: 500,
    Icon = Icons.UserFrame)]

namespace Kentico.Xperience.AdminStats.Admin;

/// <summary>
/// Member registrations over time, total members, external sign-ups and members by role.
/// </summary>
[UIEvaluatePermission(StatsPermissions.MEMBERS)]
public sealed class MembersPage(
    IMembersService membersService,
    IPageLinkGenerator pageLinkGenerator,
    TimeProvider clock,
    IUIPermissionEvaluator permissionEvaluator) : StatsReportPage<MembersClientProperties>(permissionEvaluator)
{
    public const string TEMPLATE_NAME = "@kentico/xperience-admin-stats/Members";

    private readonly IMembersService membersService = membersService;
    private readonly IPageLinkGenerator pageLinkGenerator = pageLinkGenerator;
    private readonly TimeProvider clock = clock;

    protected override async Task<MembersClientProperties> ConfigureReportProperties(MembersClientProperties properties)
    {
        var query = new StatsFilter().Normalize(GetToday());

        properties.Report = await membersService.GetReport(query, refresh: false, CancellationToken.None);
        properties.Today = GetToday();
        properties.PagePath = pageLinkGenerator.GetPath<MembersPage>();

        return properties;
    }

    [PageCommand(CommandName = "LOAD", Permission = StatsPermissions.MEMBERS)]
    public async Task<ICommandResponse<MembersResult>> Load(StatsLoadRequest request, CancellationToken cancellationToken)
    {
        var query = (request?.Filter ?? new StatsFilter()).Normalize(GetToday());
        var report = await membersService.GetReport(query, request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(report);
    }

    // MemberCreated is compared as stored (server local time), so "today" uses the server time zone.
    private DateOnly GetToday() => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
}

public sealed class MembersClientProperties : StatsReportClientProperties
{
    public MembersResult? Report { get; set; }

    /// <summary>
    /// Server date used for date presets.
    /// </summary>
    public DateOnly Today { get; set; }

    /// <summary>
    /// Path of this page relative to the admin root. The client uses it to turn admin paths into links.
    /// </summary>
    public string? PagePath { get; set; }
}
