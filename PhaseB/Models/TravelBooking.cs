namespace CsharpDiscountCalculator.PhaseB.Models;

public sealed record TravelBooking(
    IReadOnlyList<Traveler> Travelers,
    IReadOnlyList<TravelSector> SelectedSectors,
    HotelCategory? HotelCategory,
    TravelItinerary Itinerary);
