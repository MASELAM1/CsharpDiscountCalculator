namespace CsharpDiscountCalculator.PhaseB.Models;

public sealed record TravelQuoteLine(
    TravelSector Sector,
    HotelCategory? HotelCategory,
    int TravelerCount,
    decimal UnitPrice,
    decimal Subtotal,
    decimal DiscountRate,
    decimal DiscountAmount,
    decimal Total);
