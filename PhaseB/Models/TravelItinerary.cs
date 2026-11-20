namespace CsharpDiscountCalculator.PhaseB.Models;

public sealed record TravelItinerary(
    string OriginAirportCode,
    string DestinationAirportCode,
    DateTime DepartureLocalDateTime,
    DateTime ArrivalLocalDateTime);
