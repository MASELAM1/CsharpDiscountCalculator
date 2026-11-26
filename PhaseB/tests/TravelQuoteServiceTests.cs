using CsharpDiscountCalculator.PhaseB.Models;
using CsharpDiscountCalculator.PhaseB.Services;

namespace CsharpDiscountCalculator.PhaseB.Tests;

public class TravelQuoteServiceTests
{
    [Theory]
    [InlineData(TravelSector.Flight, 0.10, 900)]
    [InlineData(TravelSector.Hotel, 0.11, 1_076.90)]
    [InlineData(TravelSector.Car, 0.08, 920)]
    [InlineData(TravelSector.Parking, 0.067, 933)]
    public void Calculate_AppliesCorporateDiscountBySector(
        TravelSector sector,
        decimal expectedRate,
        decimal expectedTotal)
    {
        var service = CreateService(1_000m);

        var quote = service.Calculate(CreateBooking(
            1,
            [sector],
            "111ZAA",
            hotelCategory: sector == TravelSector.Hotel ? HotelCategory.TwoStar : null,
            hotelMealOption: sector == TravelSector.Hotel ? HotelMealOption.BreakfastAndDinner : null));

        Assert.Equal(expectedRate, quote.Lines.Single().DiscountRate);
        Assert.Equal(expectedTotal, quote.Total);
    }

    [Fact]
    public void Calculate_AllowsNonFlightBookingWithoutItinerary()
    {
        var quote = new TravelQuoteService().Calculate(new TravelBooking(
            [new Traveler("Amina", "Ndlovu")],
            [TravelSector.Car],
            null,
            null));

        Assert.Null(quote.Itinerary);
        Assert.Null(quote.Classification);
        Assert.Equal(1_800m, quote.Total);
    }

    [Fact]
    public void Calculate_RequiresItineraryWhenFlightIsSelected()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new TravelQuoteService().Calculate(new TravelBooking(
                [new Traveler("Amina", "Ndlovu")],
                [TravelSector.Flight],
                null,
                null)));

        Assert.Contains("itinerary is required", exception.Message);
    }

    [Fact]
    public void Calculate_AppliesCustomerDiscountWhenTravelerSubtotalExceedsThreshold()
    {
        var service = CreateService(7_550m);

        var quote = service.Calculate(CreateBooking(
            2,
            [TravelSector.Flight, TravelSector.Car],
            customerCode: "ABC333"));

        Assert.Equal(30_200m, quote.Subtotal);
        Assert.Equal(4_077m, quote.DiscountAmount);
        Assert.Equal(26_123m, quote.Total);
    }

    [Fact]
    public void Calculate_DoesNotApplyCustomerDiscountWhenTravelerSubtotalEqualsThreshold()
    {
        var service = CreateService(7_500m);

        var quote = service.Calculate(CreateBooking(
            2,
            [TravelSector.Flight, TravelSector.Car],
            customerCode: "ABC333"));

        Assert.Equal(30_000m, quote.Subtotal);
        Assert.Equal(0m, quote.DiscountAmount);
        Assert.Equal(30_000m, quote.Total);
    }

    [Fact]
    public void Calculate_AppliesCodesIndependentlyToEachTraveler()
    {
        var service = CreateService(20_000m);
        var travelers = new[]
        {
            new Traveler("Amina", "Ndlovu", "111ZAA"),
            new Traveler("Lebo", "Mokoena", CustomerCode: "ABC333"),
            new Traveler("Thabo", "Dlamini")
        };

        var quote = service.Calculate(new TravelBooking(
            travelers,
            [TravelSector.Flight],
            null,
            TestItinerary(),
            FlightCompany.SAA));

        Assert.Equal(60_000m, quote.Subtotal);
        Assert.Equal(4_700m, quote.DiscountAmount);
        Assert.Equal(55_300m, quote.Total);
        Assert.Equal([2_000m, 2_700m, 0m], quote.Lines.Select(line => line.DiscountAmount));
        Assert.Equal(travelers, quote.Lines.Select(line => line.Traveler));
    }

    [Fact]
    public void Calculate_UsesEachTravelersOwnSectorsAndItinerary()
    {
        var domesticItinerary = TestItinerary("JNB", "CPT");
        var internationalItinerary = TestItinerary("JNB", "LHR");
        var travelers = new[]
        {
            new Traveler(
                "Amina",
                "Ndlovu",
                TravelPreferences: new TravelerTravelPreferences(
                    [TravelSector.Flight],
                    Itinerary: domesticItinerary,
                    FlightCompany: FlightCompany.SAA)),
            new Traveler(
                "Lebo",
                "Mokoena",
                TravelPreferences: new TravelerTravelPreferences(
                    [TravelSector.Flight, TravelSector.Car],
                    Itinerary: internationalItinerary)),
            new Traveler(
                "Thabo",
                "Dlamini",
                TravelPreferences: new TravelerTravelPreferences([TravelSector.Parking]))
        };

        var quote = new TravelQuoteService().Calculate(new TravelBooking(
            travelers,
            [],
            null,
            null));

        Assert.Equal(
            [TravelSector.Flight, TravelSector.Flight, TravelSector.Car, TravelSector.Parking],
            quote.Lines.Select(line => line.Sector));
        Assert.Equal(
            [domesticItinerary, internationalItinerary, internationalItinerary, null],
            quote.Lines.Select(line => line.Itinerary));
        Assert.Equal(
            [TravelClassification.Domestic, TravelClassification.International, TravelClassification.International, null],
            quote.Lines.Select(line => line.Classification));
        Assert.Equal([3_500m, 7_500m, 1_800m, 300m], quote.Lines.Select(line => line.UnitPrice));
    }

    [Fact]
    public void Calculate_CorporateCodeTakesPrecedenceForThatTraveler()
    {
        var service = CreateService(10_000m);
        var traveler = new Traveler("Amina", "Ndlovu", "111ZAA", "ABC333");

        var quote = service.Calculate(new TravelBooking(
            [traveler],
            [TravelSector.Flight],
            null,
            TestItinerary(),
            FlightCompany.SAA));

        Assert.Equal(1_000m, quote.DiscountAmount);
        Assert.Equal(9_000m, quote.Total);
        Assert.Contains("not stacked", quote.DiscountDescription);
    }

    [Theory]
    [InlineData("JNB", "CPT", 0.12, 420)]
    [InlineData("JNB", "WDH", 0.20, 1_200)]
    [InlineData("JNB", "LHR", 0.20, 1_500)]
    public void Calculate_AppliesFrequentFlyerDiscountByFlightClassification(
        string origin,
        string destination,
        decimal expectedRate,
        decimal expectedDiscount)
    {
        var traveler = new Traveler("Amina", "Ndlovu", FrequentFlyerCode: "ZAA989343FR");
        var itinerary = TestItinerary(origin, destination);
        var flightCompany = TravelClassificationService.Classify(itinerary) == TravelClassification.Domestic
            ? FlightCompany.SAA
            : (FlightCompany?)null;

        var quote = new TravelQuoteService().Calculate(new TravelBooking(
            [traveler],
            [TravelSector.Flight],
            null,
            itinerary,
            flightCompany));

        Assert.Equal(expectedRate, quote.Lines.Single().DiscountRate);
        Assert.Equal(expectedDiscount, quote.DiscountAmount);
    }

    [Fact]
    public void Calculate_FrequentFlyerCodeTakesPrecedenceOverCustomerCode()
    {
        var service = CreateService(20_000m);
        var traveler = new Traveler(
            "Amina",
            "Ndlovu",
            CustomerCode: "ABC333",
            FrequentFlyerCode: "ZAA989343FR");

        var quote = service.Calculate(new TravelBooking(
            [traveler],
            [TravelSector.Flight, TravelSector.Car],
            null,
            TestItinerary(),
            FlightCompany.SAA));

        Assert.Equal([2_400m, 0m], quote.Lines.Select(line => line.DiscountAmount));
        Assert.Equal(2_400m, quote.DiscountAmount);
    }

    [Fact]
    public void Calculate_CorporateCodeTakesPrecedenceOverFrequentFlyerCode()
    {
        var service = CreateService(20_000m);
        var traveler = new Traveler(
            "Amina",
            "Ndlovu",
            "111ZAA",
            FrequentFlyerCode: "ZAA989343FR");

        var quote = service.Calculate(new TravelBooking(
            [traveler],
            [TravelSector.Flight, TravelSector.Car],
            null,
            TestItinerary(),
            FlightCompany.SAA));

        Assert.Equal([2_000m, 1_600m], quote.Lines.Select(line => line.DiscountAmount));
    }

    [Fact]
    public void Calculate_DoesNotApplyFrequentFlyerDiscountToDomesticAndInternationalItinerary()
    {
        var traveler = new Traveler("Amina", "Ndlovu", FrequentFlyerCode: "ZAA989343FR");
        var itinerary = new TravelItinerary(
            "JNB",
            "LHR",
            new DateTime(2026, 1, 1, 8, 0, 0),
            new DateTime(2026, 1, 1, 10, 0, 0),
            "CPT");

        var quote = new TravelQuoteService().Calculate(new TravelBooking(
            [traveler],
            [TravelSector.Flight],
            null,
            itinerary));

        Assert.Equal(TravelClassification.DomesticAndInternational, quote.Lines.Single().Classification);
        Assert.Equal(0m, quote.DiscountAmount);
    }

    [Fact]
    public void Calculate_RejectsFrequentFlyerCodeReusedByTravelers()
    {
        var travelers = new[]
        {
            new Traveler("Amina", "Ndlovu", FrequentFlyerCode: "ZAA989343FR"),
            new Traveler("Lebo", "Mokoena", FrequentFlyerCode: "zaa989343fr")
        };

        var exception = Assert.Throws<ArgumentException>(() =>
            new TravelQuoteService().Calculate(new TravelBooking(
                travelers,
                [TravelSector.Flight],
                null,
                TestItinerary(),
                FlightCompany.SAA)));

        Assert.Contains("unique frequent-flyer code", exception.Message);
    }

    [Fact]
    public void Calculate_UsesEachTravelerSubtotalForCustomerThreshold()
    {
        var service = CreateService(10_000m);
        var travelers = new[]
        {
            new Traveler("Amina", "Ndlovu", CustomerCode: "ABC333"),
            new Traveler("Lebo", "Mokoena"),
            new Traveler("Thabo", "Dlamini")
        };

        var quote = service.Calculate(new TravelBooking(
            travelers,
            [TravelSector.Flight],
            null,
            TestItinerary(),
            FlightCompany.SAA));

        Assert.Equal(30_000m, quote.Subtotal);
        Assert.Equal(0m, quote.DiscountAmount);
        Assert.Equal(30_000m, quote.Total);
    }

    [Fact]
    public void Calculate_RoundsCustomerDiscountOncePerTraveler()
    {
        var service = new TravelQuoteService(new TravelPriceCatalog(
            new Dictionary<TravelSector, decimal>
            {
                [TravelSector.Car] = 10_000.03m,
                [TravelSector.Flight] = 10_000.03m,
                [TravelSector.Parking] = 300m
            },
            new Dictionary<HotelCategory, decimal>
            {
                [HotelCategory.BedAndBreakfast] = 4_200m,
                [HotelCategory.OneStar] = 3_000m,
                [HotelCategory.TwoStar] = 4_200m,
                [HotelCategory.ThreeStar] = 5_400m,
                [HotelCategory.FourStar] = 7_000m,
                [HotelCategory.FiveStar] = 9_000m
            }));
        var traveler = new Traveler("Amina", "Ndlovu", CustomerCode: "ABC333");

        var quote = service.Calculate(new TravelBooking(
            [traveler],
            [TravelSector.Flight, TravelSector.Car],
            null,
            TestItinerary(),
            FlightCompany.SAA));

        Assert.Equal(20_000.06m, quote.Subtotal);
        Assert.Equal(2_700.01m, quote.DiscountAmount);
        Assert.Equal(17_300.05m, quote.Total);
    }

    [Fact]
    public void TravelPriceCatalog_BnbMatchesTwoStarAndHigherStarsCostMore()
    {
        var catalog = new TravelPriceCatalog();

        Assert.Equal(
            catalog.GetHotelPricePerTraveler(HotelCategory.BedAndBreakfast),
            catalog.GetHotelPricePerTraveler(HotelCategory.TwoStar));
        Assert.True(catalog.GetHotelPricePerTraveler(HotelCategory.OneStar)
            < catalog.GetHotelPricePerTraveler(HotelCategory.TwoStar));
        Assert.True(catalog.GetHotelPricePerTraveler(HotelCategory.TwoStar)
            < catalog.GetHotelPricePerTraveler(HotelCategory.ThreeStar));
        Assert.True(catalog.GetHotelPricePerTraveler(HotelCategory.ThreeStar)
            < catalog.GetHotelPricePerTraveler(HotelCategory.FourStar));
        Assert.True(catalog.GetHotelPricePerTraveler(HotelCategory.FourStar)
            < catalog.GetHotelPricePerTraveler(HotelCategory.FiveStar));
    }

    [Fact]
    public void Calculate_UsesSelectedHotelCategoryPriceAndReturnsTravelerNames()
    {
        var traveler = new Traveler("Amina", "Ndlovu", "111ZAA");
        var service = new TravelQuoteService();

        var quote = service.Calculate(new TravelBooking(
            [traveler],
            [TravelSector.Hotel],
            HotelCategory.FiveStar,
            TestItinerary(),
            HotelMealOption: HotelMealOption.DinnerOnly));

        Assert.Equal(9_155m, quote.Lines.Single().UnitPrice);
        Assert.Equal(HotelMealOption.DinnerOnly, quote.Lines.Single().HotelMealOption);
        Assert.Equal(8_147.95m, quote.Total);
        Assert.Equal([traveler], quote.Travelers);
    }

    [Theory]
    [InlineData(HotelMealOption.BreakfastAndDinner, 4_410)]
    [InlineData(HotelMealOption.BreakfastOnly, 4_310)]
    [InlineData(HotelMealOption.DinnerOnly, 4_355)]
    public void Calculate_RecordsSelectedHotelMealOptionAndAddsItsPrice(
        HotelMealOption mealOption,
        decimal expectedPrice)
    {
        var quote = new TravelQuoteService().Calculate(new TravelBooking(
            [new Traveler("Amina", "Ndlovu")],
            [TravelSector.Hotel],
            HotelCategory.TwoStar,
            TestItinerary(),
            HotelMealOption: mealOption));

        Assert.Equal(expectedPrice, quote.Lines.Single().UnitPrice);
        Assert.Equal(mealOption, quote.Lines.Single().HotelMealOption);
    }

    [Fact]
    public void Calculate_RequiresTravelerFirstAndLastNames()
    {
        var service = CreateService(1_000m);
        var booking = new TravelBooking(
            [new Traveler("Amina", "")],
            [TravelSector.Flight],
            null,
            TestItinerary(),
            FlightCompany.SAA);

        Assert.Throws<ArgumentException>(() => service.Calculate(booking));
    }

    [Fact]
    public void Calculate_RequiresHotelCategoryWhenHotelIsSelected()
    {
        var service = CreateService(1_000m);
        var booking = CreateBooking(1, [TravelSector.Hotel]);

        Assert.Throws<ArgumentException>(() => service.Calculate(booking));
    }

    [Fact]
    public void Calculate_RequiresHotelMealOptionWhenHotelIsSelected()
    {
        var booking = new TravelBooking(
            [new Traveler("Amina", "Ndlovu")],
            [TravelSector.Hotel],
            HotelCategory.TwoStar,
            TestItinerary());

        Assert.Throws<ArgumentException>(() => new TravelQuoteService().Calculate(booking));
    }

    [Theory]
    [InlineData("ABC333", true)]
    [InlineData("abc333", true)]
    [InlineData("AB333", false)]
    [InlineData("ABCD333", false)]
    [InlineData("ABC334", false)]
    [InlineData("111ZAA", false)]
    public void DiscountCodeValidator_ValidatesCustomerCodeFormat(string code, bool expected) =>
        Assert.Equal(expected, DiscountCodeValidator.IsCustomerCode(code));

    [Theory]
    [InlineData("ZAA989343FR", true)]
    [InlineData("zaa989343fr", true)]
    [InlineData("ZAA12345FR", false)]
    [InlineData("ZAA1234567FR", false)]
    [InlineData("ZAB123456FR", false)]
    [InlineData("ZAA123456FX", false)]
    [InlineData("ZAA12A456FR", false)]
    public void DiscountCodeValidator_ValidatesFrequentFlyerCodeFormat(string code, bool expected) =>
        Assert.Equal(expected, DiscountCodeValidator.IsFrequentFlyerCode(code));

    [Fact]
    public void Calculate_RejectsInvalidFrequentFlyerCode()
    {
        var traveler = new Traveler("Amina", "Ndlovu", FrequentFlyerCode: "ZAA12345FR");

        var exception = Assert.Throws<ArgumentException>(() =>
            new TravelQuoteService().Calculate(new TravelBooking(
                [traveler],
                [TravelSector.Flight],
                null,
                TestItinerary(),
                FlightCompany.SAA)));

        Assert.Contains("invalid frequent-flyer code", exception.Message);
    }

    [Fact]
    public void Calculate_RejectsInvalidCorporateCode()
    {
        var service = CreateService(1_000m);

        Assert.Throws<ArgumentException>(() =>
            service.Calculate(CreateBooking(1, [TravelSector.Flight], "111XYZ")));
    }

    [Theory]
    [InlineData("JNB", "CPT", TravelClassification.Domestic)]
    [InlineData("JNB", "WDH", TravelClassification.Regional)]
    [InlineData("JNB", "LHR", TravelClassification.International)]
    public void Calculate_ClassifiesRouteAndReturnsItinerary(
        string origin,
        string destination,
        TravelClassification expectedClassification)
    {
        var itinerary = TestItinerary(origin, destination);
        var quote = CreateService(1_000m).Calculate(
            CreateBooking(1, [TravelSector.Flight], itinerary: itinerary));

        Assert.Equal(expectedClassification, quote.Classification);
        Assert.Equal(itinerary, quote.Itinerary);
    }

    [Theory]
    [InlineData("JNB", "CPT", 3_500)]
    [InlineData("JNB", "WDH", 6_000)]
    [InlineData("JNB", "LHR", 7_500)]
    public void Calculate_UsesFlightPriceForRouteClassification(
        string origin,
        string destination,
        decimal expectedFlightPrice)
    {
        var quote = new TravelQuoteService().Calculate(
            CreateBooking(1, [TravelSector.Flight], itinerary: TestItinerary(origin, destination)));

        Assert.Equal(expectedFlightPrice, quote.Lines.Single().UnitPrice);
    }

    [Theory]
    [InlineData(FlightCompany.SAA, 8, 3_500)]
    [InlineData(FlightCompany.SAA, 12, 3_620)]
    [InlineData(FlightCompany.FlySafair, 8, 4_000)]
    [InlineData(FlightCompany.FlySafair, 12, 4_120)]
    [InlineData(FlightCompany.Airlink, 8, 4_790)]
    [InlineData(FlightCompany.Airlink, 12, 4_910)]
    public void Calculate_UsesDomesticCompanyAndDepartureTimeForFlightPrice(
        FlightCompany company,
        int departureHour,
        decimal expectedPrice)
    {
        var itinerary = TestItinerary() with
        {
            DepartureLocalDateTime = new DateTime(2026, 1, 1, departureHour, 0, 0)
        };
        var quote = new TravelQuoteService().Calculate(
            CreateBooking(1, [TravelSector.Flight], itinerary: itinerary, flightCompany: company));

        Assert.Equal(company, quote.FlightCompany);
        Assert.Equal(expectedPrice, quote.Lines.Single().UnitPrice);
    }

    [Fact]
    public void Calculate_ChargesAnotherDomesticFareForReturnFlight()
    {
        var itinerary = TestItinerary() with
        {
            ReturnDepartureLocalDateTime = new DateTime(2026, 1, 2, 12, 0, 0),
            ReturnArrivalLocalDateTime = new DateTime(2026, 1, 2, 14, 0, 0)
        };

        var quote = new TravelQuoteService().Calculate(
            CreateBooking(1, [TravelSector.Flight], itinerary: itinerary, flightCompany: FlightCompany.SAA));

        Assert.Equal(7_120m, quote.Lines.Single().UnitPrice);
        Assert.Equal(FlightCompany.SAA, quote.ReturnFlightCompany);
        Assert.Equal(itinerary, quote.Itinerary);
    }

    [Fact]
    public void Calculate_UsesSelectedCompanyAndDepartureTimeForReturnFlight()
    {
        var itinerary = TestItinerary() with
        {
            ReturnDepartureLocalDateTime = new DateTime(2026, 1, 2, 12, 0, 0),
            ReturnArrivalLocalDateTime = new DateTime(2026, 1, 2, 14, 0, 0)
        };

        var quote = new TravelQuoteService().Calculate(
            CreateBooking(
                1,
                [TravelSector.Flight],
                itinerary: itinerary,
                flightCompany: FlightCompany.SAA,
                returnFlightCompany: FlightCompany.Airlink));

        Assert.Equal(8_410m, quote.Lines.Single().UnitPrice);
        Assert.Equal(FlightCompany.SAA, quote.FlightCompany);
        Assert.Equal(FlightCompany.Airlink, quote.ReturnFlightCompany);
    }

    [Fact]
    public void Calculate_RejectsReturnCompanyWithoutReturnFlight()
    {
        var booking = CreateBooking(
            1,
            [TravelSector.Flight],
            itinerary: TestItinerary(),
            flightCompany: FlightCompany.SAA,
            returnFlightCompany: FlightCompany.Airlink);

        Assert.Throws<ArgumentException>(() => new TravelQuoteService().Calculate(booking));
    }

    [Fact]
    public void Calculate_RequiresBothReturnFlightTimes()
    {
        var itinerary = TestItinerary() with
        {
            ReturnDepartureLocalDateTime = new DateTime(2026, 1, 2, 12, 0, 0)
        };

        Assert.Throws<ArgumentException>(() => new TravelQuoteService().Calculate(
            CreateBooking(1, [TravelSector.Flight], itinerary: itinerary, flightCompany: FlightCompany.SAA)));
    }

    [Fact]
    public void Calculate_RequiresFlightSectorForReturnFlight()
    {
        var itinerary = TestItinerary() with
        {
            ReturnDepartureLocalDateTime = new DateTime(2026, 1, 2, 12, 0, 0),
            ReturnArrivalLocalDateTime = new DateTime(2026, 1, 2, 14, 0, 0)
        };

        Assert.Throws<ArgumentException>(() => new TravelQuoteService().Calculate(
            CreateBooking(1, [TravelSector.Car], itinerary: itinerary)));
    }

    [Fact]
    public void Calculate_RequiresReturnDepartureAfterOutboundArrival()
    {
        var itinerary = TestItinerary() with
        {
            ReturnDepartureLocalDateTime = new DateTime(2026, 1, 1, 9, 0, 0),
            ReturnArrivalLocalDateTime = new DateTime(2026, 1, 2, 14, 0, 0)
        };

        Assert.Throws<ArgumentException>(() => new TravelQuoteService().Calculate(
            CreateBooking(1, [TravelSector.Flight], itinerary: itinerary, flightCompany: FlightCompany.SAA)));
    }

    [Fact]
    public void Calculate_RequiresCompanyForDomesticFlight()
    {
        var booking = new TravelBooking(
            [new Traveler("Amina", "Ndlovu")],
            [TravelSector.Flight],
            null,
            TestItinerary());

        Assert.Throws<ArgumentException>(() => new TravelQuoteService().Calculate(booking));
    }

    [Fact]
    public void Calculate_UsesCombinedFlightPriceForDomesticAndInternationalItinerary()
    {
        var itinerary = new TravelItinerary(
            "JNB",
            "LHR",
            new DateTime(2026, 1, 1, 8, 0, 0),
            new DateTime(2026, 1, 1, 20, 0, 0),
            "CPT");
        var quote = new TravelQuoteService().Calculate(
            CreateBooking(1, [TravelSector.Flight], itinerary: itinerary));

        Assert.Equal(TravelClassification.DomesticAndInternational, quote.Classification);
        Assert.Equal(12_000m, quote.Lines.Single().UnitPrice);
    }

    [Fact]
    public void TravelClassificationService_RejectsUnsupportedAirportCode()
    {
        var itinerary = TestItinerary("XXX", "JNB");

        Assert.Throws<ArgumentException>(() => TravelClassificationService.Classify(itinerary));
    }

    [Theory]
    [InlineData("JNB", "CPT", "LHR")]
    [InlineData("CPT", "JNB", "DXB")]
    public void TravelClassificationService_ClassifiesDomesticThenInternationalLegs(
        string origin,
        string connection,
        string destination)
    {
        var itinerary = new TravelItinerary(
            origin,
            destination,
            new DateTime(2026, 1, 1, 8, 0, 0),
            new DateTime(2026, 1, 1, 20, 0, 0),
            connection);

        Assert.Equal(
            TravelClassification.DomesticAndInternational,
            TravelClassificationService.Classify(itinerary));
        TravelClassificationService.ValidateSelection(
            TravelClassification.DomesticAndInternational,
            itinerary);
    }

    [Fact]
    public void TravelClassificationService_RejectsTripTypeThatDoesNotMatchRoute()
    {
        var itinerary = TestItinerary("JNB", "WDH");

        Assert.Throws<ArgumentException>(() =>
            TravelClassificationService.ValidateSelection(TravelClassification.International, itinerary));
    }

    [Fact]
    public void TravelClassificationService_RejectsNonDomesticConnectionLeg()
    {
        var itinerary = new TravelItinerary(
            "JNB",
            "LHR",
            new DateTime(2026, 1, 1, 8, 0, 0),
            new DateTime(2026, 1, 1, 20, 0, 0),
            "WDH");

        Assert.Throws<ArgumentException>(() => TravelClassificationService.Classify(itinerary));
    }

    [Fact]
    public void AirportCodeCatalog_ListsSouthAfricanAirportsFirst()
    {
        var airports = AirportCodeCatalog.SupportedAirports.ToArray();
        var firstNonSouthAfricanIndex = Array.FindIndex(airports, airport => airport.CountryCode != "ZA");

        Assert.All(
            airports.Take(firstNonSouthAfricanIndex),
            airport => Assert.Equal("South Africa", airport.CountryName));
        Assert.Equal("ZA", airports[0].CountryCode);
        Assert.DoesNotContain(
            airports.Skip(firstNonSouthAfricanIndex),
            airport => airport.CountryCode == "ZA");
        Assert.Contains(airports, airport => airport.Code == "UTT" && airport.CountryCode == "ZA");
        Assert.Contains(airports, airport => airport.Code == "PTG" && airport.CountryCode == "ZA");
    }

    [Fact]
    public void DomesticFlightRouteCatalog_ContainsOnlySupportedSouthAfricanAirports()
    {
        foreach (var route in DomesticFlightRouteCatalog.SupportedRoutes)
        {
            Assert.Equal("ZA", AirportCodeCatalog.GetAirport(route.OriginAirportCode).CountryCode);
            Assert.Equal("ZA", AirportCodeCatalog.GetAirport(route.DestinationAirportCode).CountryCode);
        }

        Assert.Contains(
            DomesticFlightRouteCatalog.SupportedRoutes,
            route => route.OriginAirportCode == "JNB" && route.DestinationAirportCode == "CPT");
        Assert.Contains(
            DomesticFlightRouteCatalog.SupportedRoutes,
            route => route.OriginAirportCode == "CPT" && route.DestinationAirportCode == "JNB");
    }

    private static TravelBooking CreateBooking(
        int travelerCount,
        IReadOnlyList<TravelSector> sectors,
        string? corporateCode = null,
        string? customerCode = null,
        HotelCategory? hotelCategory = null,
        HotelMealOption? hotelMealOption = null,
        TravelItinerary? itinerary = null,
        FlightCompany? flightCompany = null,
        FlightCompany? returnFlightCompany = null)
    {
        itinerary ??= TestItinerary();
        if (flightCompany is null
            && sectors.Contains(TravelSector.Flight)
            && TravelClassificationService.Classify(itinerary) == TravelClassification.Domestic)
        {
            flightCompany = FlightCompany.SAA;
        }

        return new(
            Enumerable.Range(1, travelerCount)
                .Select(index => new Traveler($"Test{index}", $"Traveler{index}", corporateCode, customerCode))
                .ToArray(),
            sectors,
            hotelCategory,
            itinerary,
            flightCompany,
            returnFlightCompany,
            hotelMealOption);
    }

    private static TravelItinerary TestItinerary(
        string origin = "JNB",
        string destination = "CPT") =>
        new(
            origin,
            destination,
            new DateTime(2026, 1, 1, 8, 0, 0),
            new DateTime(2026, 1, 1, 10, 0, 0));

    private static TravelQuoteService CreateService(decimal price) =>
        new(new TravelPriceCatalog(
            new Dictionary<TravelSector, decimal>
            {
                [TravelSector.Car] = price,
                [TravelSector.Flight] = price,
                [TravelSector.Parking] = price
            },
            new Dictionary<HotelCategory, decimal>
            {
                [HotelCategory.BedAndBreakfast] = 1_000m,
                [HotelCategory.OneStar] = 900m,
                [HotelCategory.TwoStar] = 1_000m,
                [HotelCategory.ThreeStar] = 1_100m,
                [HotelCategory.FourStar] = 1_200m,
                [HotelCategory.FiveStar] = 1_300m
            }));
}
