using CsharpDiscountCalculator.PhaseB.Models;

namespace CsharpDiscountCalculator.PhaseB.Services;

public sealed class TravelPriceCatalog
{
    private readonly IReadOnlyDictionary<TravelSector, decimal> _pricesPerTraveler;
    private readonly IReadOnlyDictionary<HotelCategory, decimal> _hotelPricesPerTraveler;
    private readonly IReadOnlyDictionary<TravelClassification, decimal> _flightPricesPerTraveler;
    private readonly IReadOnlyDictionary<FlightCompany, decimal> _domesticFlightPricesPerTraveler;

    public TravelPriceCatalog()
        : this(new Dictionary<TravelSector, decimal>
        {
            [TravelSector.Car] = 1_800m,
            [TravelSector.Flight] = 7_500m,
            [TravelSector.Parking] = 300m
        }, new Dictionary<HotelCategory, decimal>
        {
            [HotelCategory.BedAndBreakfast] = 4_200m,
            [HotelCategory.OneStar] = 3_000m,
            [HotelCategory.TwoStar] = 4_200m,
            [HotelCategory.ThreeStar] = 5_400m,
            [HotelCategory.FourStar] = 7_000m,
            [HotelCategory.FiveStar] = 9_000m
        }, new Dictionary<TravelClassification, decimal>
        {
            [TravelClassification.Domestic] = 3_500m,
            [TravelClassification.Regional] = 6_000m,
            [TravelClassification.International] = 7_500m,
            [TravelClassification.DomesticAndInternational] = 12_000m
        }, new Dictionary<FlightCompany, decimal>
        {
            [FlightCompany.SAA] = 3_500m,
            [FlightCompany.FlySafair] = 4_000m,
            [FlightCompany.Airlink] = 4_790m
        })
    {
    }

    public TravelPriceCatalog(
        IReadOnlyDictionary<TravelSector, decimal> pricesPerTraveler,
        IReadOnlyDictionary<HotelCategory, decimal> hotelPricesPerTraveler,
        IReadOnlyDictionary<TravelClassification, decimal>? flightPricesPerTraveler = null,
        IReadOnlyDictionary<FlightCompany, decimal>? domesticFlightPricesPerTraveler = null)
    {
        ArgumentNullException.ThrowIfNull(pricesPerTraveler);
        ArgumentNullException.ThrowIfNull(hotelPricesPerTraveler);

        foreach (var sector in Enum.GetValues<TravelSector>().Where(sector => sector != TravelSector.Hotel))
        {
            if (!pricesPerTraveler.TryGetValue(sector, out var price) || price < 0)
            {
                throw new ArgumentException($"A non-negative price is required for {sector}.", nameof(pricesPerTraveler));
            }
        }

        foreach (var category in Enum.GetValues<HotelCategory>())
        {
            if (!hotelPricesPerTraveler.TryGetValue(category, out var price) || price < 0)
            {
                throw new ArgumentException($"A non-negative price is required for {category}.", nameof(hotelPricesPerTraveler));
            }
        }

        flightPricesPerTraveler ??= Enum.GetValues<TravelClassification>()
            .ToDictionary(classification => classification, _ => pricesPerTraveler[TravelSector.Flight]);
        foreach (var classification in Enum.GetValues<TravelClassification>())
        {
            if (!flightPricesPerTraveler.TryGetValue(classification, out var price) || price < 0)
            {
                throw new ArgumentException(
                    $"A non-negative flight price is required for {classification}.",
                    nameof(flightPricesPerTraveler));
            }
        }

        domesticFlightPricesPerTraveler ??= Enum.GetValues<FlightCompany>()
            .ToDictionary(company => company, _ => flightPricesPerTraveler[TravelClassification.Domestic]);
        foreach (var company in Enum.GetValues<FlightCompany>())
        {
            if (!domesticFlightPricesPerTraveler.TryGetValue(company, out var price) || price < 0)
            {
                throw new ArgumentException(
                    $"A non-negative domestic flight price is required for {company}.",
                    nameof(domesticFlightPricesPerTraveler));
            }
        }

        if (hotelPricesPerTraveler[HotelCategory.BedAndBreakfast] != hotelPricesPerTraveler[HotelCategory.TwoStar]
            || hotelPricesPerTraveler[HotelCategory.OneStar] >= hotelPricesPerTraveler[HotelCategory.TwoStar]
            || hotelPricesPerTraveler[HotelCategory.TwoStar] >= hotelPricesPerTraveler[HotelCategory.ThreeStar]
            || hotelPricesPerTraveler[HotelCategory.ThreeStar] >= hotelPricesPerTraveler[HotelCategory.FourStar]
            || hotelPricesPerTraveler[HotelCategory.FourStar] >= hotelPricesPerTraveler[HotelCategory.FiveStar])
        {
            throw new ArgumentException(
                "Hotel prices must increase by star level, with B&B priced the same as a 2-star hotel.",
                nameof(hotelPricesPerTraveler));
        }

        _pricesPerTraveler = pricesPerTraveler.ToDictionary(pair => pair.Key, pair => pair.Value);
        _hotelPricesPerTraveler = hotelPricesPerTraveler.ToDictionary(pair => pair.Key, pair => pair.Value);
        _flightPricesPerTraveler = flightPricesPerTraveler.ToDictionary(pair => pair.Key, pair => pair.Value);
        _domesticFlightPricesPerTraveler = domesticFlightPricesPerTraveler.ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    public decimal GetPricePerTraveler(
        TravelSector sector,
        HotelCategory? hotelCategory = null,
        TravelClassification? travelClassification = null,
        FlightCompany? flightCompany = null,
        DateTime? departureLocalDateTime = null)
    {
        if (flightCompany is not null
            && (sector != TravelSector.Flight || travelClassification != TravelClassification.Domestic))
        {
            throw new ArgumentException("A flight company can only be used for a domestic flight.");
        }

        if (travelClassification is not null && sector != TravelSector.Flight)
        {
            throw new ArgumentException("A travel classification can only be used with the Flight sector.");
        }

        if (sector == TravelSector.Hotel)
        {
            return hotelCategory is { } category && _hotelPricesPerTraveler.TryGetValue(category, out var hotelPrice)
                ? hotelPrice
                : throw new ArgumentException("Select a hotel category.");
        }

        if (hotelCategory is not null)
        {
            throw new ArgumentException("A hotel category can only be used with the Hotel sector.");
        }

        if (sector == TravelSector.Flight && travelClassification is { } classification)
        {
            if (classification == TravelClassification.Domestic && flightCompany is { } company)
            {
                if (departureLocalDateTime is null)
                {
                    throw new ArgumentException(
                        "A departure time is required to price a domestic flight.",
                        nameof(departureLocalDateTime));
                }

                if (!_domesticFlightPricesPerTraveler.TryGetValue(company, out var companyPrice))
                {
                    throw new ArgumentOutOfRangeException(nameof(flightCompany), company, "Unknown flight company.");
                }

                return departureLocalDateTime.Value.Hour < 12 ? companyPrice : companyPrice + 120m;
            }

            return _flightPricesPerTraveler.TryGetValue(classification, out var flightPrice)
                ? flightPrice
                : throw new ArgumentOutOfRangeException(
                    nameof(travelClassification),
                    classification,
                    "Unknown travel classification.");
        }

        return _pricesPerTraveler.TryGetValue(sector, out var price)
            ? price
            : throw new ArgumentOutOfRangeException(nameof(sector), sector, "Unknown travel sector.");
    }

    public decimal GetHotelPricePerTraveler(HotelCategory category) =>
        _hotelPricesPerTraveler.TryGetValue(category, out var price)
            ? price
            : throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown hotel category.");
}
