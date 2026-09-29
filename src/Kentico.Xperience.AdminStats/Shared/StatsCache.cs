using CMS.Helpers;

namespace Kentico.Xperience.AdminStats.Shared;

/// <summary>
/// Cache names shared by the reports.
/// Cached report data has no cache dependencies: it is dropped only by expiry or an explicit refresh,
/// so data changes (for example activity logging on busy sites) never clear it.
/// </summary>
internal static class StatsCache
{
    public const string ItemNamePrefix = "kentico.xperience.adminstats";
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
