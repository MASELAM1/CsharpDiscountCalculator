namespace CsharpDiscountCalculator.PhaseB.Models;

public sealed record TravelerTravelPreferences(
    IReadOnlyList<TravelSector> SelectedSectors,
    HotelCategory? HotelCategory = null,
    HotelMealOption? HotelMealOption = null,
    TravelItinerary? Itinerary = null,
    FlightCompany? FlightCompany = null,
    FlightCompany? ReturnFlightCompany = null);
