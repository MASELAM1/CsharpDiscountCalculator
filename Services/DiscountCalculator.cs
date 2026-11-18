using System.Globalization;

namespace CsharpDiscountCalculator;

public sealed class DiscountCalculator
{
    public DiscountResult Calculate(decimal price)
    {
        if (price < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Prices cannot be negative.");
        }

        decimal discountedPrice = price switch
        {
            > 20 and < 50 => Round(price * 0.90m),
            >= 50 => Round(price * 0.75m),
            _ => Round(price)
        };

        return new DiscountResult(Round(price), discountedPrice);
    }

    public IReadOnlyList<DiscountResult> Calculate(IEnumerable<decimal> prices)
    {
        ArgumentNullException.ThrowIfNull(prices);

        return prices
            .Select(Calculate)
            .OrderBy(result => result.DiscountedPrice)
            .ThenBy(result => result.OriginalPrice)
            .ToArray();
    }

    public IReadOnlyList<DiscountResult> Calculate(LinkedList<decimal> prices)
    {
        ArgumentNullException.ThrowIfNull(prices);
        return Calculate(prices.AsEnumerable());
    }

    public static IReadOnlyList<decimal> ParsePrices(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("Enter at least one price.", nameof(input));
        }

        var parts = input.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var prices = new List<decimal>(parts.Length);

        foreach (var part in parts)
        {
            if (!decimal.TryParse(part, NumberStyles.Number, CultureInfo.InvariantCulture, out var price))
            {
                throw new FormatException($"'{part}' is not a valid price.");
            }

            if (price < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(input), "Prices cannot be negative.");
            }

            prices.Add(Round(price));
        }

        return prices;
    }

    private static decimal Round(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
