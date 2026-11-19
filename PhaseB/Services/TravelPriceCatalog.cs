using CsharpDiscountCalculator.PhaseB.Models;

namespace CsharpDiscountCalculator.PhaseB.Services;

public sealed class TravelPriceCatalog
{
    private readonly IReadOnlyDictionary<TravelSector, decimal> _pricesPerTraveler;
    private readonly IReadOnlyDictionary<HotelCategory, decimal> _hotelPricesPerTraveler;

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
        })
    {
    }

    public TravelPriceCatalog(
        IReadOnlyDictionary<TravelSector, decimal> pricesPerTraveler,
        IReadOnlyDictionary<HotelCategory, decimal> hotelPricesPerTraveler)
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
    }

    public decimal GetPricePerTraveler(TravelSector sector, HotelCategory? hotelCategory = null)
    {
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

        return _pricesPerTraveler.TryGetValue(sector, out var price)
            ? price
            : throw new ArgumentOutOfRangeException(nameof(sector), sector, "Unknown travel sector.");
    }

    public decimal GetHotelPricePerTraveler(HotelCategory category) =>
        _hotelPricesPerTraveler.TryGetValue(category, out var price)
            ? price
            : throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown hotel category.");
}
