using CMS.ContentEngine;
using CMS.DataEngine;

namespace Kentico.Xperience.AdminStats.Shared;

/// <summary>
/// Channel shown in the channel filter.
/// </summary>
/// <param name="Id">Channel ID.</param>
/// <param name="DisplayName">Channel display name.</param>
/// <param name="Type">Channel type name (Website or Email).</param>
public sealed record StatsChannelOption(int Id, string DisplayName, string Type);

/// <summary>
/// Provides channel options for the shared channel filter.
/// </summary>
public interface IStatsChannelOptionsProvider
{
    public Task<IReadOnlyList<StatsChannelOption>> GetChannelOptions(CancellationToken cancellationToken);
}

internal sealed class StatsChannelOptionsProvider(IInfoProvider<ChannelInfo> channelProvider) : IStatsChannelOptionsProvider
{
    private readonly IInfoProvider<ChannelInfo> channelProvider = channelProvider;

    public async Task<IReadOnlyList<StatsChannelOption>> GetChannelOptions(CancellationToken cancellationToken)
    {
        var channels = await channelProvider
            .Get()
            .Columns(
                nameof(ChannelInfo.ChannelID),
                nameof(ChannelInfo.ChannelDisplayName),
                nameof(ChannelInfo.ChannelType))
            .GetEnumerableTypedResultAsync(cancellationToken: cancellationToken);

        return
        [
            .. channels
                .Where(c => c.ChannelType is ChannelType.Website or ChannelType.Email)
                .OrderBy(c => c.ChannelType)
                .ThenBy(c => c.ChannelDisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(c => new StatsChannelOption(c.ChannelID, c.ChannelDisplayName, c.ChannelType.ToString()))
        ];
    }
}
