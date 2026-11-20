namespace CsharpDiscountCalculator.PhaseB.Services;

public sealed record Airport(string Code, string Name, string CountryCode, string CountryName);

public static class AirportCodeCatalog
{
    private static readonly IReadOnlyDictionary<string, Airport> Airports = CreateAirports();

    public static IReadOnlyCollection<Airport> SupportedAirports =>
        Airports.Values
            .OrderBy(airport => airport.CountryCode == "ZA" ? 0 : 1)
            .ThenBy(airport => airport.CountryName)
            .ThenBy(airport => airport.Code)
            .ToArray();

    public static Airport GetAirport(string? code)
    {
        var normalizedCode = code?.Trim().ToUpperInvariant();
        if (normalizedCode is not null && Airports.TryGetValue(normalizedCode, out var airport))
        {
            return airport;
        }

        throw new ArgumentException($"Airport code '{code}' is not supported.");
    }

    private static IReadOnlyDictionary<string, Airport> CreateAirports()
    {
        var airports = new Dictionary<string, Airport>(StringComparer.OrdinalIgnoreCase);
        Add(airports, "ZA", "South Africa", "JNB:Johannesburg, CPT:Cape Town, DUR:Durban, PLZ:Gqeberha, ELS:East London, BFN:Bloemfontein, HLA:Lanseria, GRJ:George, KIM:Kimberley, MQP:Mbombela, SZK:Skukuza, UTN:Upington, RCB:Richards Bay, PZB:Pietermaritzburg, HDS:Hoedspruit, UTT:Mthatha, PTG:Polokwane, MGH:Margate, SIS:Sishen");
        Add(airports, "AO", "Angola", "LAD:Luanda");
        Add(airports, "BW", "Botswana", "GBE:Gaborone, MUB:Maun");
        Add(airports, "KM", "Comoros", "HAH:Moroni");
        Add(airports, "CD", "Democratic Republic of the Congo", "FIH:Kinshasa, FBM:Lubumbashi");
        Add(airports, "SZ", "Eswatini", "SHO:Manzini");
        Add(airports, "LS", "Lesotho", "MSU:Maseru");
        Add(airports, "MG", "Madagascar", "TNR:Antananarivo");
        Add(airports, "MW", "Malawi", "LLW:Lilongwe, BLZ:Blantyre");
        Add(airports, "MU", "Mauritius", "MRU:Port Louis");
        Add(airports, "MZ", "Mozambique", "MPM:Maputo, BEW:Beira, APL:Nampula");
        Add(airports, "NA", "Namibia", "WDH:Windhoek, WAL:Walvis Bay, LUD:Luderitz");
        Add(airports, "SC", "Seychelles", "SEZ:Mahe");
        Add(airports, "TZ", "Tanzania", "DAR:Dar es Salaam, JRO:Kilimanjaro, ZNZ:Zanzibar");
        Add(airports, "ZM", "Zambia", "LUN:Lusaka, NLA:Ndola");
        Add(airports, "ZW", "Zimbabwe", "HRE:Harare, VFA:Victoria Falls, BUQ:Bulawayo");

        Add(airports, "AE", "United Arab Emirates", "DXB:Dubai, AUH:Abu Dhabi");
        Add(airports, "AU", "Australia", "SYD:Sydney, MEL:Melbourne");
        Add(airports, "BR", "Brazil", "GRU:Sao Paulo");
        Add(airports, "CA", "Canada", "YYZ:Toronto, YVR:Vancouver");
        Add(airports, "CH", "Switzerland", "ZRH:Zurich");
        Add(airports, "DE", "Germany", "FRA:Frankfurt, MUC:Munich");
        Add(airports, "EG", "Egypt", "CAI:Cairo");
        Add(airports, "ET", "Ethiopia", "ADD:Addis Ababa");
        Add(airports, "FR", "France", "CDG:Paris");
        Add(airports, "GB", "United Kingdom", "LHR:London Heathrow, LGW:London Gatwick, MAN:Manchester");
        Add(airports, "GH", "Ghana", "ACC:Accra");
        Add(airports, "HK", "Hong Kong", "HKG:Hong Kong");
        Add(airports, "IN", "India", "BOM:Mumbai, DEL:Delhi");
        Add(airports, "IT", "Italy", "FCO:Rome");
        Add(airports, "JP", "Japan", "HND:Tokyo Haneda, NRT:Tokyo Narita");
        Add(airports, "KE", "Kenya", "NBO:Nairobi");
        Add(airports, "NG", "Nigeria", "LOS:Lagos");
        Add(airports, "NL", "Netherlands", "AMS:Amsterdam");
        Add(airports, "QA", "Qatar", "DOH:Doha");
        Add(airports, "SG", "Singapore", "SIN:Singapore");
        Add(airports, "TR", "Turkey", "IST:Istanbul");
        Add(airports, "US", "United States", "ATL:Atlanta, EWR:Newark, IAD:Washington Dulles, JFK:New York, LAX:Los Angeles");

        return airports;
    }

    private static void Add(
        IDictionary<string, Airport> airports,
        string countryCode,
        string countryName,
        string airportList)
    {
        foreach (var airportEntry in airportList.Split(", ", StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = airportEntry.Split(':', 2);
            airports.Add(
                parts[0],
                new Airport(parts[0], parts[1], countryCode, countryName));
        }
    }
}
