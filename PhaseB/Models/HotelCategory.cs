namespace CsharpDiscountCalculator.PhaseB.Models;

public enum HotelCategory
{
    BedAndBreakfast,
    OneStar,
    TwoStar,
    ThreeStar,
    FourStar,
    FiveStar
}

public static class HotelCategoryExtensions
{
    public static string ToDisplayName(this HotelCategory category) =>
        category switch
        {
            HotelCategory.BedAndBreakfast => "B&B",
            HotelCategory.OneStar => "1-star hotel",
            HotelCategory.TwoStar => "2-star hotel",
            HotelCategory.ThreeStar => "3-star hotel",
            HotelCategory.FourStar => "4-star hotel",
            HotelCategory.FiveStar => "5-star hotel",
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown hotel category.")
        };
}
