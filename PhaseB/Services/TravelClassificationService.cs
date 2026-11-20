using CsharpDiscountCalculator.PhaseB.Models;

namespace CsharpDiscountCalculator.PhaseB.Services;

public static class TravelClassificationService
{
    private static readonly HashSet<string> RegionalCountryCodes =
    [
        "AO", "BW", "KM", "CD", "SZ", "LS", "MG", "MW",
        "MU", "MZ", "NA", "SC", "ZA", "TZ", "ZM", "ZW"
    ];

    public static TravelClassification Classify(TravelItinerary itinerary)
    {
        ArgumentNullException.ThrowIfNull(itinerary);

        var origin = AirportCodeCatalog.GetAirport(itinerary.OriginAirportCode);
        var destination = AirportCodeCatalog.GetAirport(itinerary.DestinationAirportCode);
        if (origin.Code == destination.Code)
        {
            throw new ArgumentException("The origin and destination airports must be different.");
        }

        if (!string.IsNullOrWhiteSpace(itinerary.ConnectionAirportCode))
        {
            var connection = AirportCodeCatalog.GetAirport(itinerary.ConnectionAirportCode);
            if (origin.Code == connection.Code || connection.Code == destination.Code)
            {
                throw new ArgumentException("Each itinerary leg must use different airports.");
            }

            if (origin.CountryCode != connection.CountryCode)
            {
                throw new ArgumentException("The origin and connection airports must be in the same country for a domestic leg.");
            }

            if (connection.CountryCode == destination.CountryCode
                || RegionalCountryCodes.Contains(connection.CountryCode)
                && RegionalCountryCodes.Contains(destination.CountryCode))
            {
                throw new ArgumentException("The connection-to-destination leg must be international.");
            }

            return TravelClassification.DomesticAndInternational;
        }

        if (origin.CountryCode == destination.CountryCode)
        {
            return TravelClassification.Domestic;
        }

        return RegionalCountryCodes.Contains(origin.CountryCode)
            && RegionalCountryCodes.Contains(destination.CountryCode)
                ? TravelClassification.Regional
                : TravelClassification.International;
    }

    public static bool IsRegionalCountry(string countryCode) =>
        RegionalCountryCodes.Contains(countryCode);

    public static void ValidateSelection(TravelClassification selectedClassification, TravelItinerary itinerary)
    {
        var actualClassification = Classify(itinerary);
        if (selectedClassification != actualClassification)
        {
            throw new ArgumentException(
                $"The selected trip type '{selectedClassification.ToDisplayName()}' does not match the airports in the route.");
        }
    }
}
