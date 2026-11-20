namespace CsharpDiscountCalculator.PhaseB.Models;

public sealed record TravelItinerary(
    string OriginAirportCode,
    string DestinationAirportCode,
    DateTime DepartureLocalDateTime,
    DateTime ArrivalLocalDateTime,
    string? ConnectionAirportCode = null,
    DateTime? ReturnDepartureLocalDateTime = null,
    DateTime? ReturnArrivalLocalDateTime = null);
