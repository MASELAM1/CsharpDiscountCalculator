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

        var corporateCode = booking.CorporateCode?.Trim();
        var customerCode = booking.CustomerCode?.Trim();
        var hasCorporateCode = !string.IsNullOrEmpty(corporateCode);
        var hasCustomerCode = !string.IsNullOrEmpty(customerCode);

        if (hasCorporateCode && !DiscountCodeValidator.IsCorporateCode(corporateCode))
        {
            throw new ArgumentException("The corporate code is not valid.");
        }

        if (hasCustomerCode && !DiscountCodeValidator.IsCustomerCode(customerCode))
        {
            throw new ArgumentException("Customer codes must contain three letters followed by 333.");
        }

        var rates = CorporateDiscountRates();
        var baseLines = booking.SelectedSectors
            .Select(sector =>
            {
                var hotelCategory = sector == TravelSector.Hotel ? booking.HotelCategory : null;
                var unitPrice = _priceCatalog.GetPricePerTraveler(sector, hotelCategory);
                var subtotal = Round(unitPrice * booking.Travelers.Count);
                return (Sector: sector, HotelCategory: hotelCategory, UnitPrice: unitPrice, Subtotal: subtotal);
            })
            .ToArray();
        var subtotalTotal = Round(baseLines.Sum(line => line.Subtotal));

        if (hasCorporateCode)
        {
            var lines = baseLines.Select(line =>
            {
                var rate = rates[line.Sector];
                var discount = Round(line.Subtotal * rate);
                return new TravelQuoteLine(
                    line.Sector,
                    line.HotelCategory,
                    booking.Travelers.Count,
                    line.UnitPrice,
                    line.Subtotal,
                    rate,
                    discount,
                    Round(line.Subtotal - discount));
            }).ToArray();

            var discountTotal = Round(lines.Sum(line => line.DiscountAmount));
            var description = hasCustomerCode
                ? "Corporate sector discounts applied; customer discount is not stacked."
                : "Corporate sector discounts applied.";

            return new TravelQuote(
                booking.Travelers,
                lines,
                subtotalTotal,
                discountTotal,
                Round(subtotalTotal - discountTotal),
                description);
        }

        var customerDiscount = hasCustomerCode && subtotalTotal > CustomerDiscountMinimumSubtotal
            ? Round(subtotalTotal * CustomerDiscountRate)
            : 0m;
        var customerDescription = customerDiscount > 0
            ? "Customer discount of 13.5% applied to the booking subtotal."
            : hasCustomerCode
                ? "Customer code accepted, but the subtotal must be above R15,000 for a discount."
                : "No discount code supplied.";
        var customerLines = baseLines
            .Select(line => new TravelQuoteLine(
                line.Sector,
                line.HotelCategory,
                booking.Travelers.Count,
                line.UnitPrice,
                line.Subtotal,
                0m,
                0m,
                line.Subtotal))
            .ToArray();

        return new TravelQuote(
            booking.Travelers,
            customerLines,
            subtotalTotal,
            customerDiscount,
            Round(subtotalTotal - customerDiscount),
            customerDescription);
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
