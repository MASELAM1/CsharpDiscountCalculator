namespace CsharpDiscountCalculator.PhaseB.Services;

public sealed record DomesticFlightRoute(string OriginAirportCode, string DestinationAirportCode)
{
    public string DisplayName =>
        $"{OriginAirportCode} — {AirportCodeCatalog.GetAirport(OriginAirportCode).Name} to " +
        $"{DestinationAirportCode} — {AirportCodeCatalog.GetAirport(DestinationAirportCode).Name}";
}

public static class DomesticFlightRouteCatalog
{
    private static readonly IReadOnlyCollection<DomesticFlightRoute> Routes = CreateRoutes();

    public static IReadOnlyCollection<DomesticFlightRoute> SupportedRoutes => Routes;

    private static IReadOnlyCollection<DomesticFlightRoute> CreateRoutes()
    {
        var airportPairs = new (string First, string Second)[]
        {
            ("JNB", "CPT"),
            ("JNB", "DUR"),
            ("JNB", "PLZ"),
            ("JNB", "ELS"),
            ("JNB", "BFN"),
            ("JNB", "HLA"),
            ("JNB", "GRJ"),
            ("JNB", "KIM"),
            ("JNB", "MQP"),
            ("JNB", "SZK"),
            ("JNB", "UTN"),
            ("JNB", "RCB"),
            ("JNB", "PZB"),
            ("JNB", "HDS"),
            ("JNB", "UTT"),
            ("JNB", "PTG"),
            ("JNB", "MGH"),
            ("JNB", "SIS"),
            ("CPT", "DUR"),
            ("CPT", "PLZ"),
            ("CPT", "GRJ"),
            ("DUR", "PLZ")
        };

        return airportPairs
            .SelectMany(pair => new[]
            {
                new DomesticFlightRoute(pair.First, pair.Second),
                new DomesticFlightRoute(pair.Second, pair.First)
            })
            .OrderBy(route => route.OriginAirportCode)
            .ThenBy(route => route.DestinationAirportCode)
            .ToArray();
    }
}
