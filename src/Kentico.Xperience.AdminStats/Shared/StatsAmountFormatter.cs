using CMS.Commerce;

namespace Kentico.Xperience.AdminStats.Shared;

/// <summary>
/// Formats <see cref="StatsValueKind.Amount"/> values with the project's price formatter, so reports show the project's currency.
/// </summary>
internal interface IStatsAmountFormatter
{
    /// <summary>
    /// Returns the formatted amount, or <c>null</c> when no price formatter is available (the admin client then formats a plain number).
    /// </summary>
    public string? Format(decimal amount);
}

/// <summary>
/// Uses the project's <see cref="IPriceFormatter"/> (the last registered one) the same way the native Orders application does:
/// <c>Format(price, new PriceFormatContext())</c>. The product registers a default formatter (<c>F2</c>, no currency);
/// projects replace it to add their currency (for example DancingGoat's <c>PriceFormatter</c>).
/// </summary>
internal sealed class StatsAmountFormatter(IEnumerable<IPriceFormatter> priceFormatters) : IStatsAmountFormatter
{
    private readonly IPriceFormatter? priceFormatter = priceFormatters.LastOrDefault();

    public string? Format(decimal amount)
    {
        if (priceFormatter is null)
        {
            return null;
        }

        try
        {
            string formatted = priceFormatter.Format(amount, new PriceFormatContext());
            return string.IsNullOrWhiteSpace(formatted) ? null : formatted;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A failing custom formatter must not break the report; the client shows plain numbers instead.
            return null;
        }
    }
}

/// <summary>
/// Adds formatted texts (<see cref="IStatsAmountFormatter"/>) to the amount values of shared report types.
/// Only <see cref="StatsValueKind.Amount"/> values get texts; counts and ratios are formatted by the client.
/// Texts are left out of the JSON when <c>null</c>, so reports without amounts send the same data as before.
/// </summary>
internal static class StatsAmountTexts
{
    public static StatsValueComparison WithTexts(this StatsValueComparison comparison, IStatsAmountFormatter formatter)
    {
        if (comparison.Kind != StatsValueKind.Amount)
        {
            return comparison;
        }

        string? Format(decimal? amount) => amount is decimal a ? formatter.Format(a) : null;

        return comparison with { CurrentText = Format(comparison.Current), PreviousText = Format(comparison.Previous) };
    }

    public static StatsValueSeries WithTexts(this StatsValueSeries series, IStatsAmountFormatter formatter)
    {
        if (series.Kind != StatsValueKind.Amount)
        {
            return series;
        }

        var texts = series.Values.Select(formatter.Format).ToList();

        // All or nothing, so the client never mixes formatted and plain values in one series.
        return texts.Any(t => t is null)
            ? series
            : series with { Texts = [.. texts.Select(t => t!)], TotalText = formatter.Format(series.Total) };
    }

    public static StatsRankedResult WithTexts(this StatsRankedResult result, IStatsAmountFormatter formatter)
    {
        bool value = result.ValueKind == StatsValueKind.Amount;
        bool secondary = result.SecondaryValueKind == StatsValueKind.Amount;
        bool tertiary = result.TertiaryValueKind == StatsValueKind.Amount;
        if (!value && !secondary && !tertiary)
        {
            return result;
        }

        string? FormatOptional(bool isAmount, decimal? amount, string? current) =>
            isAmount && amount is decimal a ? formatter.Format(a) : current;

        var items = result.Items
            .Select(item => item with
            {
                ValueText = FormatOptional(value, item.Value, item.ValueText),
                PreviousValueText = FormatOptional(value, item.PreviousValue, item.PreviousValueText),
                SecondaryValueText = FormatOptional(secondary, item.SecondaryValue, item.SecondaryValueText),
                TertiaryValueText = FormatOptional(tertiary, item.TertiaryValue, item.TertiaryValueText),
            })
            .ToList();

        return result with
        {
            Items = items,
            TotalText = value ? formatter.Format(result.Total) : result.TotalText,
        };
    }
}
