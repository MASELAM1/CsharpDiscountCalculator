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

        if (origin.CountryCode == destination.CountryCode)
        {
            return TravelClassification.Domestic;
        }

        return RegionalCountryCodes.Contains(origin.CountryCode)
            && RegionalCountryCodes.Contains(destination.CountryCode)
                ? TravelClassification.Regional
                : TravelClassification.International;
    }
}
