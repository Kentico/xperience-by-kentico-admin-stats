namespace Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

/// <summary>
/// Input of a report <c>LOAD</c> page command.
/// </summary>
public sealed record StatsLoadRequest
{
    /// <summary>
    /// Report filter. <c>null</c> means defaults.
    /// </summary>
    public StatsFilter? Filter { get; init; }

    /// <summary>
    /// When <c>true</c>, cached data for the filter is dropped and read again from the database.
    /// </summary>
    public bool Refresh { get; init; }
}
