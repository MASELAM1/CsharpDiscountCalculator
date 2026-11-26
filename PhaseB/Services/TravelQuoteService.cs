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

        var rates = CorporateDiscountRates();
        var lines = new List<TravelQuoteLine>();
        var customerCodeCount = 0;
        var corporateCodeCount = 0;
        var customerDiscountCount = 0;
        var frequentFlyerCodeCount = 0;
        var frequentFlyerDiscountCount = 0;
        var frequentFlyerCodes = booking.Travelers
            .Select(traveler => traveler.FrequentFlyerCode?.Trim())
            .Where(code => !string.IsNullOrEmpty(code))
            .ToArray();
        if (frequentFlyerCodes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != frequentFlyerCodes.Length)
        {
            throw new ArgumentException("Each traveler must have a unique frequent-flyer code.");
        }

        foreach (var (traveler, travelerIndex) in booking.Travelers.Select((traveler, index) => (traveler, index)))
        {
            var preferences = traveler.TravelPreferences;
            var sectors = preferences?.SelectedSectors ?? booking.SelectedSectors;
            ArgumentNullException.ThrowIfNull(sectors);
            if (sectors.Count == 0)
            {
                throw new ArgumentException($"Select at least one travel sector for traveler {travelerIndex + 1}.");
            }

            if (sectors.Distinct().Count() != sectors.Count)
            {
                throw new ArgumentException(
                    $"Each travel sector can only be selected once for traveler {travelerIndex + 1}.");
            }

            var hotelCategory = preferences is null ? booking.HotelCategory : preferences.HotelCategory;
            var hotelMealOption = preferences is null ? booking.HotelMealOption : preferences.HotelMealOption;
            var itinerary = preferences is null ? booking.Itinerary : preferences.Itinerary;
            var flightCompany = preferences is null ? booking.FlightCompany : preferences.FlightCompany;
            var selectedReturnFlightCompany = preferences is null
                ? booking.ReturnFlightCompany
                : preferences.ReturnFlightCompany;
            var includesFlight = sectors.Contains(TravelSector.Flight);
            var includesHotel = sectors.Contains(TravelSector.Hotel);
            if (includesFlight && itinerary is null)
            {
                throw new ArgumentException(
                    $"A flight itinerary is required when traveler {travelerIndex + 1} selects the Flight sector.",
                    nameof(booking.Itinerary));
            }

            TravelClassification? classification = itinerary is null
                ? null
                : TravelClassificationService.Classify(itinerary);
            if (flightCompany is { } selectedCompany && !Enum.IsDefined(selectedCompany))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(booking.FlightCompany),
                    selectedCompany,
                    "Unknown flight company.");
            }

            if (selectedReturnFlightCompany is { } selectedReturnCompany && !Enum.IsDefined(selectedReturnCompany))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(booking.ReturnFlightCompany),
                    selectedReturnCompany,
                    "Unknown return flight company.");
            }

            if (itinerary is not null)
            {
                ValidateReturnFlight(itinerary, includesFlight);
            }

            if (includesFlight && classification == TravelClassification.Domestic && flightCompany is null)
            {
                throw new ArgumentException($"Choose a flight company for traveler {travelerIndex + 1}'s domestic flight.");
            }

            if (flightCompany is not null && (!includesFlight || classification != TravelClassification.Domestic))
            {
                throw new ArgumentException("A flight company can only be selected for a domestic flight.");
            }

            var includesReturnFlight = itinerary?.ReturnDepartureLocalDateTime is not null;
            if (selectedReturnFlightCompany is not null
                && (!includesReturnFlight || classification != TravelClassification.Domestic))
            {
                throw new ArgumentException("A return flight company can only be selected for a domestic return flight.");
            }

            var returnFlightCompany = includesReturnFlight && classification == TravelClassification.Domestic
                ? selectedReturnFlightCompany ?? flightCompany
                : null;

            if (includesHotel && hotelCategory is null)
            {
                throw new ArgumentException($"Choose a hotel category for traveler {travelerIndex + 1}.");
            }

            if (!includesHotel && hotelCategory is not null)
            {
                throw new ArgumentException("Choose a hotel category only when the Hotel sector is selected.");
            }

            if (includesHotel && hotelMealOption is null)
            {
                throw new ArgumentException($"Choose a hotel meal option for traveler {travelerIndex + 1}.");
            }

            if (!includesHotel && hotelMealOption is not null)
            {
                throw new ArgumentException("Choose a hotel meal option only when the Hotel sector is selected.");
            }

            if (hotelMealOption is { } mealOption && !Enum.IsDefined(mealOption))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(booking.HotelMealOption),
                    mealOption,
                    "Unknown hotel meal option.");
            }

            var corporateCode = traveler.CorporateCode?.Trim();
            var customerCode = traveler.CustomerCode?.Trim();
            var frequentFlyerCode = traveler.FrequentFlyerCode?.Trim();
            var hasCorporateCode = !string.IsNullOrEmpty(corporateCode);
            var hasCustomerCode = !string.IsNullOrEmpty(customerCode);
            var hasFrequentFlyerCode = !string.IsNullOrEmpty(frequentFlyerCode);

            if (hasCorporateCode && !DiscountCodeValidator.IsCorporateCode(corporateCode))
            {
                throw new ArgumentException($"Traveler {travelerIndex + 1} has an invalid corporate code.");
            }

            if (hasCustomerCode && !DiscountCodeValidator.IsCustomerCode(customerCode))
            {
                throw new ArgumentException($"Traveler {travelerIndex + 1} has an invalid customer code.");
            }

            if (hasFrequentFlyerCode && !DiscountCodeValidator.IsFrequentFlyerCode(frequentFlyerCode))
            {
                throw new ArgumentException($"Traveler {travelerIndex + 1} has an invalid frequent-flyer code.");
            }

            corporateCodeCount += hasCorporateCode ? 1 : 0;
            customerCodeCount += hasCustomerCode ? 1 : 0;
            frequentFlyerCodeCount += hasFrequentFlyerCode ? 1 : 0;

            var travelerLines = sectors
                .Select(sector =>
                {
                    var selectedHotelCategory = sector == TravelSector.Hotel ? hotelCategory : null;
                    var selectedHotelMealOption = sector == TravelSector.Hotel ? hotelMealOption : null;
                    var unitPrice = _priceCatalog.GetPricePerTraveler(
                        sector,
                        selectedHotelCategory,
                        sector == TravelSector.Flight ? classification : null,
                        sector == TravelSector.Flight ? flightCompany : null,
                        sector == TravelSector.Flight
                            ? itinerary!.DepartureLocalDateTime
                            : null,
                        selectedHotelMealOption);
                    if (sector == TravelSector.Flight
                        && itinerary?.ReturnDepartureLocalDateTime is { } returnDeparture)
                    {
                        unitPrice += _priceCatalog.GetPricePerTraveler(
                            sector,
                            travelClassification: classification,
                            flightCompany: returnFlightCompany,
                            departureLocalDateTime: returnDeparture);
                    }

                    return (
                        Sector: sector,
                        HotelCategory: selectedHotelCategory,
                        HotelMealOption: selectedHotelMealOption,
                        UnitPrice: unitPrice,
                        Subtotal: Round(unitPrice));
                })
                .ToArray();
            var travelerSubtotal = Round(travelerLines.Sum(line => line.Subtotal));
            var hasFrequentFlyerDiscount = hasFrequentFlyerCode
                && travelerLines.Any(line =>
                    line.Sector == TravelSector.Flight
                    && FrequentFlyerDiscountRate(classification) > 0m);
            frequentFlyerDiscountCount += hasFrequentFlyerDiscount ? 1 : 0;
            var hasCustomerDiscount = !hasCorporateCode
                && !hasFrequentFlyerDiscount
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
                decimal rate;
                decimal discount;
                if (hasCorporateCode)
                {
                    rate = rates[line.Sector];
                    discount = Round(line.Subtotal * rate);
                }
                else if (hasFrequentFlyerDiscount && line.Sector == TravelSector.Flight)
                {
                    rate = FrequentFlyerDiscountRate(classification);
                    discount = Round(line.Subtotal * rate);
                }
                else if (hasCustomerDiscount)
                {
                    rate = CustomerDiscountRate;
                    discount = lineIndex == travelerLines.Length - 1
                        ? travelerCustomerDiscount - allocatedCustomerDiscount
                        : Round(travelerCustomerDiscount * line.Subtotal / travelerSubtotal);
                    allocatedCustomerDiscount += discount;
                }
                else
                {
                    rate = 0m;
                    discount = 0m;
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
                    Round(line.Subtotal - discount),
                    itinerary,
                    classification,
                    flightCompany,
                    returnFlightCompany));
            }
        }

        var subtotalTotal = Round(lines.Sum(line => line.Subtotal));
        var discountTotal = Round(lines.Sum(line => line.DiscountAmount));
        var total = Round(lines.Sum(line => line.Total));
        var hasMultipleDiscountCodeTypes = new[]
        {
            corporateCodeCount > 0,
            frequentFlyerCodeCount > 0,
            customerCodeCount > 0
        }.Count(hasCodes => hasCodes) > 1;
        var description = hasMultipleDiscountCodeTypes
            ? "Discount codes are evaluated per traveler in this order: corporate, frequent-flyer, then customer. Discounts are not stacked."
            : corporateCodeCount > 0
                ? "Corporate sector discounts applied only to travelers with a valid corporate code."
                : frequentFlyerDiscountCount > 0
                    ? "Frequent-flyer discounts applied to eligible domestic, regional, and international flight lines."
                    : frequentFlyerCodeCount > 0
                        ? "Frequent-flyer codes accepted, but no eligible flight discounts were applied."
                : customerDiscountCount > 0
                    ? "Customer discounts of 13.5% applied to eligible traveler subtotals above R15,000."
                    : customerCodeCount > 0
                        ? "Customer codes accepted, but no traveler subtotal is above R15,000."
                        : "No discount codes supplied.";

        var hasTravelerSpecificPreferences = booking.Travelers.Any(traveler => traveler.TravelPreferences is not null);
        TravelClassification? legacyClassification = booking.Itinerary is null
            ? null
            : TravelClassificationService.Classify(booking.Itinerary);
        var legacyReturnFlightCompany = booking.Itinerary?.ReturnDepartureLocalDateTime is not null
            && legacyClassification == TravelClassification.Domestic
            ? booking.ReturnFlightCompany ?? booking.FlightCompany
            : null;

        return new TravelQuote(
            booking.Travelers,
            lines,
            subtotalTotal,
            discountTotal,
            total,
            description,
            hasTravelerSpecificPreferences ? null : booking.Itinerary,
            hasTravelerSpecificPreferences ? null : legacyClassification,
            hasTravelerSpecificPreferences ? null : booking.FlightCompany,
            hasTravelerSpecificPreferences ? null : legacyReturnFlightCompany);
    }

    private static IReadOnlyDictionary<TravelSector, decimal> CorporateDiscountRates() =>
        new Dictionary<TravelSector, decimal>
        {
            [TravelSector.Flight] = 0.10m,
            [TravelSector.Hotel] = 0.11m,
            [TravelSector.Car] = 0.08m,
            [TravelSector.Parking] = 0.067m
        };

    private static decimal FrequentFlyerDiscountRate(TravelClassification? classification) =>
        classification switch
        {
            TravelClassification.Domestic => 0.12m,
            TravelClassification.Regional or TravelClassification.International => 0.20m,
            _ => 0m
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
