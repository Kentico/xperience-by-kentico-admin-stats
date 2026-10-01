using CMS.ContentEngine;
using CMS.DataEngine;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

/// <summary>
/// Channel shown in the channel filter.
/// </summary>
/// <param name="Id">Channel ID.</param>
/// <param name="DisplayName">Channel display name.</param>
/// <param name="Type">Channel type name (<see cref="ChannelType"/>, for example Website or Email).</param>
public sealed record StatsChannelOption(int Id, string DisplayName, string Type);

/// <summary>
/// Provides channel options for the shared channel filter.
/// </summary>
public interface IStatsChannelOptionsProvider
{
    /// <summary>
    /// Returns website and email channels (the channels activities are logged for).
    /// </summary>
    public Task<IReadOnlyList<StatsChannelOption>> GetChannelOptions(CancellationToken cancellationToken);

    /// <summary>
    /// Returns channels of the given types, ordered by type (in the order of <paramref name="types"/>), then display name.
    /// </summary>
    public Task<IReadOnlyList<StatsChannelOption>> GetChannelOptions(IReadOnlyList<ChannelType> types, CancellationToken cancellationToken);
}

internal sealed class StatsChannelOptionsProvider(IInfoProvider<ChannelInfo> channelProvider) : IStatsChannelOptionsProvider
{
    private static readonly ChannelType[] activityChannelTypes = [ChannelType.Website, ChannelType.Email];

    private readonly IInfoProvider<ChannelInfo> channelProvider = channelProvider;

    public Task<IReadOnlyList<StatsChannelOption>> GetChannelOptions(CancellationToken cancellationToken) =>
        GetChannelOptions(activityChannelTypes, cancellationToken);

    public async Task<IReadOnlyList<StatsChannelOption>> GetChannelOptions(IReadOnlyList<ChannelType> types, CancellationToken cancellationToken)
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
                .Where(c => types.Contains(c.ChannelType))
                .OrderBy(c => IndexOf(types, c.ChannelType))
                .ThenBy(c => c.ChannelDisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(c => new StatsChannelOption(c.ChannelID, c.ChannelDisplayName, c.ChannelType.ToString()))
        ];
    }

    private static int IndexOf(IReadOnlyList<ChannelType> types, ChannelType type)
    {
        for (int i = 0; i < types.Count; i++)
        {
            if (types[i] == type)
            {
                return i;
            }
        }

        return types.Count;
    }
}
