using CsharpDiscountCalculator;

namespace CsharpDiscountCalculator.Tests;

public class DiscountCalculatorTests
{
    private readonly DiscountCalculator _calculator = new();

    [Theory]
    [InlineData("20", "20")]
    [InlineData("20.01", "18.01")]
    [InlineData("49.99", "44.99")]
    [InlineData("50", "37.50")]
    [InlineData("67.50", "50.63")]
    public void Calculate_AppliesExpectedDiscount(string input, string expected)
    {
        var price = decimal.Parse(input);
        var expectedPrice = decimal.Parse(expected);

        var result = _calculator.Calculate(price);

        Assert.Equal(expectedPrice, result.DiscountedPrice);
    }

    [Fact]
    public void Calculate_SortsDiscountedPricesAscending()
    {
        decimal[] prices = [67.50m, 12.30m, 33.89m, 9m];

        var results = _calculator.Calculate(prices);

        Assert.Equal([9m, 12.30m, 30.50m, 50.63m],
            results.Select(result => result.DiscountedPrice));
    }

    [Fact]
    public void Calculate_LinkedListProducesSortedResults()
    {
        var prices = new LinkedList<decimal>();
        prices.AddLast(56.10m);
        prices.AddLast(19m);
        prices.AddLast(44m);

        var results = _calculator.Calculate(prices);

        Assert.Equal([19m, 39.60m, 42.08m],
            results.Select(result => result.DiscountedPrice));
    }

    [Fact]
    public void Calculate_RejectsNegativePrice()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.Calculate(-1m));
    }

    [Fact]
    public void ParsePrices_SupportsCommaAndSpaceSeparatedInput()
    {
        var prices = DiscountCalculator.ParsePrices("12.30,33.89 56.10");

        Assert.Equal([12.30m, 33.89m, 56.10m], prices);
    }

    [Fact]
    public void ParsePrices_RejectsInvalidInput()
    {
        Assert.Throws<FormatException>(() => DiscountCalculator.ParsePrices("12.30,abc"));
    }
}
