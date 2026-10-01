using System.Text.Json.Serialization;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

/// <summary>
/// Period size used to bucket trend data.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<StatsGrouping>))]
public enum StatsGrouping
{
    Day,
    Week,
    Month,
}
