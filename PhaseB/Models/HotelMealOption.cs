namespace CsharpDiscountCalculator.PhaseB.Models;

public enum HotelMealOption
{
    BreakfastAndDinner,
    BreakfastOnly,
    DinnerOnly
}

public static class HotelMealOptionExtensions
{
    public static string ToDisplayName(this HotelMealOption mealOption) =>
        mealOption switch
        {
            HotelMealOption.BreakfastAndDinner => "Breakfast + Dinner",
            HotelMealOption.BreakfastOnly => "Breakfast only",
            HotelMealOption.DinnerOnly => "Dinner only",
            _ => throw new ArgumentOutOfRangeException(nameof(mealOption), mealOption, "Unknown hotel meal option.")
        };
}
