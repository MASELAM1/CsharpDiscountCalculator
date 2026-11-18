namespace CsharpDiscountCalculator;

public sealed record DiscountResult(decimal OriginalPrice, decimal DiscountedPrice)
{
    public decimal DiscountAmount => OriginalPrice - DiscountedPrice;
}
