namespace CsharpDiscountCalculator.PhaseB.Models;

public sealed record TravelQuoteLine(
    Traveler Traveler,
    TravelSector Sector,
    HotelCategory? HotelCategory,
    HotelMealOption? HotelMealOption,
    decimal UnitPrice,
    decimal Subtotal,
    decimal DiscountRate,
    decimal DiscountAmount,
    decimal Total);
