namespace CsharpDiscountCalculator.PhaseB.Models;

public sealed record TravelBooking(
    IReadOnlyList<Traveler> Travelers,
    IReadOnlyList<TravelSector> SelectedSectors,
    HotelCategory? HotelCategory,
    TravelItinerary? Itinerary,
    FlightCompany? FlightCompany = null,
    FlightCompany? ReturnFlightCompany = null,
    HotelMealOption? HotelMealOption = null);
