namespace Kentico.Xperience.AdminStats.Shared;

/// <summary>
/// "x of y" row: how many of a total have something (for example content items with a language variant).
/// </summary>
/// <param name="Key">Stable identifier of the row (for example a language code name). Unique in the list.</param>
/// <param name="Label">Label shown in charts and tables.</param>
/// <param name="SecondaryLabel">Optional extra text (for example the language code name).</param>
/// <param name="Covered">Number that have it.</param>
/// <param name="Total">Number that could have it.</param>
public sealed record StatsCoverageItem(string Key, string Label, string? SecondaryLabel, int Covered, int Total)
{
    /// <summary>
    /// Number that do not have it (<see cref="Total"/> - <see cref="Covered"/>, never below 0).
    /// </summary>
    public int Missing => Math.Max(Total - Covered, 0);

    /// <summary>
    /// <see cref="Covered"/> as a share of <see cref="Total"/> (0-1). 0 when the total is 0.
    /// </summary>
    public double Share => Total > 0 ? Math.Min((double)Covered / Total, 1) : 0;
}
