using System.Globalization;
using System.Text.Json;

using CMS.Commerce;

using Kentico.Xperience.AdminStats.Shared;

namespace Kentico.Xperience.AdminStats.Tests;

public class StatsAmountFormatterTests
{
    private static readonly StatsQuery range = new(new(2026, 9, 1), new(2026, 9, 30), StatsGrouping.Day, null);

    [Test]
    public void Format_UsesLastRegisteredPriceFormatter_WithContext()
    {
        var first = new FakePriceFormatter("A");
        var last = new FakePriceFormatter("B");

        string? text = new StatsAmountFormatter([first, last]).Format(12.5m);

        Assert.That(text, Is.EqualTo("B12.50"));
        Assert.That(last.LastContext, Is.Not.Null);
        Assert.That(first.Calls, Is.Zero);
    }

    [Test]
    public void Format_NoPriceFormatter_ReturnsNull() =>
        Assert.That(new StatsAmountFormatter([]).Format(12.5m), Is.Null);

    [Test]
    public void Format_FailingOrEmptyFormatter_ReturnsNull()
    {
        Assert.That(new StatsAmountFormatter([new FakePriceFormatter("x") { Throw = true }]).Format(1m), Is.Null);
        Assert.That(new StatsAmountFormatter([new FakePriceFormatter("x") { Empty = true }]).Format(1m), Is.Null);
    }

    [Test]
    public void WithTexts_Comparison_AmountsOnly_NullValueNoText()
    {
        var formatter = new StatsAmountFormatter([new FakePriceFormatter("$")]);

        var amount = StatsValueComparison.Create(range, StatsValueKind.Amount, 5m, null).WithTexts(formatter);
        var count = StatsValueComparison.Create(range, StatsValueKind.Count, 5m, 1m).WithTexts(formatter);

        Assert.That(amount.CurrentText, Is.EqualTo("$5.00"));
        Assert.That(amount.PreviousText, Is.Null);
        Assert.That(count.CurrentText, Is.Null);
    }

    [Test]
    public void WithTexts_Series_AllOrNothing()
    {
        var series = new StatsValueSeries("revenue", "Revenue", StatsValueKind.Amount, [1m, 2m], 3m);

        var formatted = series.WithTexts(new StatsAmountFormatter([new FakePriceFormatter("$")]));
        var plain = series.WithTexts(new StatsAmountFormatter([]));

        Assert.That(formatted.Texts, Is.EqualTo(new[] { "$1.00", "$2.00" }));
        Assert.That(formatted.TotalText, Is.EqualTo("$3.00"));
        Assert.That(plain.Texts, Is.Null);
        Assert.That(plain.TotalText, Is.Null);
    }

    [Test]
    public void WithTexts_RankedWithoutAmounts_Unchanged_AndNoTextsInJson()
    {
        var result = StatsRankedBuilder.Build(range, [new StatsRankedEntry("a", "A", null, 3, 1, null) { PreviousValue = 1 }], 3, 1, 10);

        var same = result.WithTexts(new StatsAmountFormatter([new FakePriceFormatter("$")]));

        Assert.That(same, Is.SameAs(result));
        string json = JsonSerializer.Serialize(same);
        Assert.That(json, Does.Not.Contain("Text"));
    }

    private sealed class FakePriceFormatter(string prefix) : IPriceFormatter
    {
        public bool Throw { get; init; }

        public bool Empty { get; init; }

        public int Calls { get; private set; }

        public PriceFormatContext? LastContext { get; private set; }

        public string Format(decimal price, PriceFormatContext context)
        {
            Calls++;
            LastContext = context;
            if (Throw)
            {
                throw new InvalidOperationException("Formatter failed.");
            }

            return Empty ? string.Empty : prefix + price.ToString("F2", CultureInfo.InvariantCulture);
        }
    }
}
