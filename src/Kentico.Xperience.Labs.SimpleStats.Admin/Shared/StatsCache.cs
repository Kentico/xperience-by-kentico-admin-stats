using CMS.Helpers;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

/// <summary>
/// Cache names and rules shared by the reports.
/// Cached report data has no cache dependencies: it is dropped only by expiry or an explicit refresh,
/// so data changes (for example activity logging on busy sites) never clear it.
/// </summary>
internal static class StatsCache
{
    public const string ItemNamePrefix = "kentico.xperience.labs.simplestats";

    /// <summary>
    /// Cache expiry. Only this expiry or an explicit refresh drops cached data; data changes do not.
    /// </summary>
    public const double CacheMinutes = 5;

    /// <summary>
    /// Returns cache settings with the shared expiry, no dependencies, and a name built from the prefix and <paramref name="nameParts"/>.
    /// </summary>
    public static CacheSettings CreateSettings(params object[] nameParts) =>
        new(CacheMinutes, [ItemNamePrefix, .. nameParts]);

    /// <summary>
    /// Loads cached data. When <paramref name="refresh"/> is <c>true</c>, the cached item is dropped first,
    /// so the data is read again and cached again.
    /// </summary>
    public static Task<TData> LoadAsync<TData>(
        this IProgressiveCache cache,
        IStatsCacheInvalidator cacheInvalidator,
        CacheSettings settings,
        bool refresh,
        Func<CancellationToken, Task<TData>> load,
        CancellationToken cancellationToken)
    {
        if (refresh)
        {
            cacheInvalidator.Remove(settings);
        }

        return cache.LoadAsync((_, token) => load(token), settings, cancellationToken);
    }
}

/// <summary>
/// Removes cached report data so the next load reads the database.
/// </summary>
internal interface IStatsCacheInvalidator
{
    public void Remove(CacheSettings settings);
}

internal sealed class StatsCacheInvalidator : IStatsCacheInvalidator
{
    public void Remove(CacheSettings settings) => CacheHelper.Remove(settings.CacheItemName);
}
