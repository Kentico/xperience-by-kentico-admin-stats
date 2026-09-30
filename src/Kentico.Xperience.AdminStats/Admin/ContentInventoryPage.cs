using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.AdminStats.Admin;
using Kentico.Xperience.AdminStats.Reports.ContentInventory;
using Kentico.Xperience.AdminStats.Shared;

[assembly: UIPage(
    uiPageType: typeof(ContentInventoryPage),
    parentType: typeof(StatsApplicationPage),
    slug: "content-inventory",
    name: "Content inventory",
    templateName: ContentInventoryPage.TEMPLATE_NAME,
    order: 500,
    Icon = Icons.Boxes)]

namespace Kentico.Xperience.AdminStats.Admin;

/// <summary>
/// Current state of content items by content type, status and language.
/// </summary>
[UIEvaluatePermission(StatsPermissions.CONTENT_INVENTORY)]
public sealed class ContentInventoryPage(
    IContentInventoryService contentInventoryService,
    IStatsChannelOptionsProvider channelOptionsProvider,
    IPageLinkGenerator pageLinkGenerator) : Page<ContentInventoryClientProperties>
{
    public const string TEMPLATE_NAME = "@kentico/xperience-admin-stats/ContentInventory";

    private readonly IContentInventoryService contentInventoryService = contentInventoryService;
    private readonly IStatsChannelOptionsProvider channelOptionsProvider = channelOptionsProvider;
    private readonly IPageLinkGenerator pageLinkGenerator = pageLinkGenerator;

    public override async Task<ContentInventoryClientProperties> ConfigureTemplateProperties(ContentInventoryClientProperties properties)
    {
        var query = new StatsSnapshotFilter().Normalize(ContentInventoryReportBuilder.Kinds);

        properties.Report = await contentInventoryService.GetReport(query, refresh: false, CancellationToken.None);
        properties.Channels = await channelOptionsProvider.GetChannelOptions(ContentInventoryReportBuilder.ChannelTypes, CancellationToken.None);
        properties.PagePath = pageLinkGenerator.GetPath<ContentInventoryPage>();

        return properties;
    }

    [PageCommand(CommandName = "LOAD", Permission = StatsPermissions.CONTENT_INVENTORY)]
    public async Task<ICommandResponse<ContentInventoryResult>> Load(StatsSnapshotLoadRequest request, CancellationToken cancellationToken)
    {
        // A channel that does not fit the kind (for example an email channel with pages) is dropped.
        var channels = await channelOptionsProvider.GetChannelOptions(ContentInventoryReportBuilder.ChannelTypes, cancellationToken);
        var query = (request?.Filter ?? new StatsSnapshotFilter()).Normalize(
            ContentInventoryReportBuilder.Kinds,
            (kind, channelId) => ContentInventoryReportBuilder.IsChannelAllowed(kind, channelId, channels));
        var report = await contentInventoryService.GetReport(query, request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(report);
    }
}

public sealed class ContentInventoryClientProperties : TemplateClientProperties
{
    public ContentInventoryResult? Report { get; set; }

    /// <summary>
    /// Website, email and headless channels. The client shows the ones that match the selected kind.
    /// </summary>
    public IReadOnlyList<StatsChannelOption> Channels { get; set; } = [];

    /// <summary>
    /// Path of this page relative to the admin root. The client uses it to turn admin paths into links.
    /// </summary>
    public string? PagePath { get; set; }
}
