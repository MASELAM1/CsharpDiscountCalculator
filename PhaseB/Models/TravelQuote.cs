namespace CsharpDiscountCalculator.PhaseB.Models;

public sealed record TravelQuote(
    IReadOnlyList<Traveler> Travelers,
    IReadOnlyList<TravelQuoteLine> Lines,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal Total,
    string DiscountDescription,
    TravelItinerary Itinerary,
    TravelClassification Classification,
    FlightCompany? FlightCompany,
    FlightCompany? ReturnFlightCompany = null);
