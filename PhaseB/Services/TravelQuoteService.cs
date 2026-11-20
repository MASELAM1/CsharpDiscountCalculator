using CsharpDiscountCalculator.PhaseB.Models;

namespace CsharpDiscountCalculator.PhaseB.Services;

public sealed class TravelQuoteService
{
    private const decimal CustomerDiscountRate = 0.135m;
    private const decimal CustomerDiscountMinimumSubtotal = 15_000m;
    private readonly TravelPriceCatalog _priceCatalog;

    public TravelQuoteService(TravelPriceCatalog? priceCatalog = null)
    {
        _priceCatalog = priceCatalog ?? new TravelPriceCatalog();
    }

    public TravelQuote Calculate(TravelBooking booking)
    {
        ArgumentNullException.ThrowIfNull(booking);
        ArgumentNullException.ThrowIfNull(booking.Itinerary);
        var classification = TravelClassificationService.Classify(booking.Itinerary);

        ArgumentNullException.ThrowIfNull(booking.Travelers);
        if (booking.Travelers.Count == 0)
        {
            throw new ArgumentException("Add at least one traveler.");
        }

        if (booking.Travelers.Any(traveler =>
            traveler is null
            || string.IsNullOrWhiteSpace(traveler.FirstName)
            || string.IsNullOrWhiteSpace(traveler.LastName)))
        {
            throw new ArgumentException("Enter a first and last name for every traveler.");
        }

        ArgumentNullException.ThrowIfNull(booking.SelectedSectors);
        if (booking.SelectedSectors.Count == 0)
        {
            throw new ArgumentException("Select at least one travel sector.");
        }

        if (booking.SelectedSectors.Distinct().Count() != booking.SelectedSectors.Count)
        {
            throw new ArgumentException("Each travel sector can only be selected once.");
        }

        var includesHotel = booking.SelectedSectors.Contains(TravelSector.Hotel);
        if (includesHotel && booking.HotelCategory is null)
        {
            throw new ArgumentException("Choose a hotel category.");
        }

        if (!includesHotel && booking.HotelCategory is not null)
        {
            throw new ArgumentException("Choose a hotel category only when the Hotel sector is selected.");
        }

        var rates = CorporateDiscountRates();
        var lines = new List<TravelQuoteLine>();
        var customerCodeCount = 0;
        var corporateCodeCount = 0;
        var customerDiscountCount = 0;
        foreach (var (traveler, travelerIndex) in booking.Travelers.Select((traveler, index) => (traveler, index)))
        {
            var corporateCode = traveler.CorporateCode?.Trim();
            var customerCode = traveler.CustomerCode?.Trim();
            var hasCorporateCode = !string.IsNullOrEmpty(corporateCode);
            var hasCustomerCode = !string.IsNullOrEmpty(customerCode);

            if (hasCorporateCode && !DiscountCodeValidator.IsCorporateCode(corporateCode))
            {
                throw new ArgumentException($"Traveler {travelerIndex + 1} has an invalid corporate code.");
            }

            if (hasCustomerCode && !DiscountCodeValidator.IsCustomerCode(customerCode))
            {
                throw new ArgumentException($"Traveler {travelerIndex + 1} has an invalid customer code.");
            }

            corporateCodeCount += hasCorporateCode ? 1 : 0;
            customerCodeCount += hasCustomerCode ? 1 : 0;

            var travelerLines = booking.SelectedSectors
                .Select(sector =>
                {
                    var hotelCategory = sector == TravelSector.Hotel ? booking.HotelCategory : null;
                    var unitPrice = _priceCatalog.GetPricePerTraveler(sector, hotelCategory);
                    return (Sector: sector, HotelCategory: hotelCategory, UnitPrice: unitPrice, Subtotal: Round(unitPrice));
                })
                .ToArray();
            var travelerSubtotal = Round(travelerLines.Sum(line => line.Subtotal));
            var hasCustomerDiscount = !hasCorporateCode
                && hasCustomerCode
                && travelerSubtotal > CustomerDiscountMinimumSubtotal;
            var travelerCustomerDiscount = hasCustomerDiscount
                ? Round(travelerSubtotal * CustomerDiscountRate)
                : 0m;
            customerDiscountCount += hasCustomerDiscount ? 1 : 0;

            var allocatedCustomerDiscount = 0m;
            for (var lineIndex = 0; lineIndex < travelerLines.Length; lineIndex++)
            {
                var line = travelerLines[lineIndex];
                var rate = hasCorporateCode
                    ? rates[line.Sector]
                    : hasCustomerDiscount ? CustomerDiscountRate : 0m;
                var discount = hasCorporateCode
                    ? Round(line.Subtotal * rate)
                    : hasCustomerDiscount && lineIndex == travelerLines.Length - 1
                        ? travelerCustomerDiscount - allocatedCustomerDiscount
                        : hasCustomerDiscount
                            ? Round(travelerCustomerDiscount * line.Subtotal / travelerSubtotal)
                            : 0m;
                if (!hasCorporateCode && hasCustomerDiscount)
                {
                    allocatedCustomerDiscount += discount;
                }

                lines.Add(new TravelQuoteLine(
                    traveler,
                    line.Sector,
                    line.HotelCategory,
                    line.UnitPrice,
                    line.Subtotal,
                    rate,
                    discount,
                    Round(line.Subtotal - discount)));
            }
        }

        var subtotalTotal = Round(lines.Sum(line => line.Subtotal));
        var discountTotal = Round(lines.Sum(line => line.DiscountAmount));
        var total = Round(lines.Sum(line => line.Total));
        var description = corporateCodeCount > 0 && customerCodeCount > 0
            ? "Individual discounts applied by traveler; corporate discounts take precedence and are not stacked with customer discounts."
            : corporateCodeCount > 0
                ? "Corporate sector discounts applied only to travelers with a valid corporate code."
                : customerDiscountCount > 0
                    ? "Customer discounts of 13.5% applied to eligible traveler subtotals above R15,000."
                    : customerCodeCount > 0
                        ? "Customer codes accepted, but no traveler subtotal is above R15,000."
                        : "No discount codes supplied.";

        return new TravelQuote(
            booking.Travelers,
            lines,
            subtotalTotal,
            discountTotal,
            total,
            description,
            booking.Itinerary,
            classification);
    }

    private static IReadOnlyDictionary<TravelSector, decimal> CorporateDiscountRates() =>
        new Dictionary<TravelSector, decimal>
        {
            [TravelSector.Flight] = 0.10m,
            [TravelSector.Hotel] = 0.11m,
            [TravelSector.Car] = 0.08m,
            [TravelSector.Parking] = 0.067m
        };

    private static decimal Round(decimal amount) =>
        decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}
