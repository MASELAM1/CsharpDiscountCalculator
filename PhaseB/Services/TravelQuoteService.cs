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
        if (booking.FlightCompany is { } selectedCompany && !Enum.IsDefined(selectedCompany))
        {
            throw new ArgumentOutOfRangeException(
                nameof(booking.FlightCompany),
                selectedCompany,
                "Unknown flight company.");
        }

        if (booking.ReturnFlightCompany is { } selectedReturnCompany && !Enum.IsDefined(selectedReturnCompany))
        {
            throw new ArgumentOutOfRangeException(
                nameof(booking.ReturnFlightCompany),
                selectedReturnCompany,
                "Unknown return flight company.");
        }

        ArgumentNullException.ThrowIfNull(booking.SelectedSectors);
        var includesFlight = booking.SelectedSectors.Contains(TravelSector.Flight);
        ValidateReturnFlight(booking.Itinerary, includesFlight);
        if (includesFlight && classification == TravelClassification.Domestic && booking.FlightCompany is null)
        {
            throw new ArgumentException("Choose a flight company for a domestic flight.");
        }

        if (booking.FlightCompany is not null
            && (!includesFlight || classification != TravelClassification.Domestic))
        {
            throw new ArgumentException("A flight company can only be selected for a domestic flight.");
        }

        var includesReturnFlight = booking.Itinerary.ReturnDepartureLocalDateTime is not null;
        if (booking.ReturnFlightCompany is not null
            && (!includesReturnFlight || classification != TravelClassification.Domestic))
        {
            throw new ArgumentException("A return flight company can only be selected for a domestic return flight.");
        }

        var returnFlightCompany = includesReturnFlight && classification == TravelClassification.Domestic
            ? booking.ReturnFlightCompany ?? booking.FlightCompany
            : null;

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

        if (includesHotel && booking.HotelMealOption is null)
        {
            throw new ArgumentException("Choose a hotel meal option.");
        }

        if (!includesHotel && booking.HotelMealOption is not null)
        {
            throw new ArgumentException("Choose a hotel meal option only when the Hotel sector is selected.");
        }

        if (booking.HotelMealOption is { } mealOption && !Enum.IsDefined(mealOption))
        {
            throw new ArgumentOutOfRangeException(
                nameof(booking.HotelMealOption),
                mealOption,
                "Unknown hotel meal option.");
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
                    var hotelMealOption = sector == TravelSector.Hotel ? booking.HotelMealOption : null;
                    var unitPrice = _priceCatalog.GetPricePerTraveler(
                        sector,
                        hotelCategory,
                        sector == TravelSector.Flight ? classification : null,
                        sector == TravelSector.Flight ? booking.FlightCompany : null,
                        sector == TravelSector.Flight ? booking.Itinerary.DepartureLocalDateTime : null,
                        hotelMealOption);
                    if (sector == TravelSector.Flight
                        && booking.Itinerary.ReturnDepartureLocalDateTime is { } returnDeparture)
                    {
                        unitPrice += _priceCatalog.GetPricePerTraveler(
                            sector,
                            travelClassification: classification,
                            flightCompany: returnFlightCompany,
                            departureLocalDateTime: returnDeparture);
                    }

                    return (
                        Sector: sector,
                        HotelCategory: hotelCategory,
                        HotelMealOption: hotelMealOption,
                        UnitPrice: unitPrice,
                        Subtotal: Round(unitPrice));
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
                    line.HotelMealOption,
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
            classification,
            booking.FlightCompany,
            returnFlightCompany);
    }

    private static IReadOnlyDictionary<TravelSector, decimal> CorporateDiscountRates() =>
        new Dictionary<TravelSector, decimal>
        {
            [TravelSector.Flight] = 0.10m,
            [TravelSector.Hotel] = 0.11m,
            [TravelSector.Car] = 0.08m,
            [TravelSector.Parking] = 0.067m
        };

    private static void ValidateReturnFlight(TravelItinerary itinerary, bool includesFlight)
    {
        var hasReturnDeparture = itinerary.ReturnDepartureLocalDateTime is not null;
        var hasReturnArrival = itinerary.ReturnArrivalLocalDateTime is not null;
        if (hasReturnDeparture != hasReturnArrival)
        {
            throw new ArgumentException("Enter both return departure and arrival date and times.");
        }

        if (!hasReturnDeparture)
        {
            return;
        }

        if (!includesFlight)
        {
            throw new ArgumentException("A return flight can only be selected when the Flight sector is selected.");
        }

        if (itinerary.ReturnDepartureLocalDateTime is { } returnDeparture
            && returnDeparture <= itinerary.ArrivalLocalDateTime)
        {
            throw new ArgumentException("Return departure must be after the outbound arrival.");
        }

        if (itinerary.ReturnArrivalLocalDateTime is { } returnArrival
            && returnArrival <= itinerary.DepartureLocalDateTime)
        {
            throw new ArgumentException("Return arrival must be after the outbound departure.");
        }
    }

    private static decimal Round(decimal amount) =>
        decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}
