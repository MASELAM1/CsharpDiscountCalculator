using CsharpDiscountCalculator.PhaseB.Models;
using CsharpDiscountCalculator.PhaseB.Services;

namespace CsharpDiscountCalculator.PhaseB.Tests;

public class TravelQuoteServiceTests
{
    [Theory]
    [InlineData(TravelSector.Flight, 0.10, 900)]
    [InlineData(TravelSector.Hotel, 0.11, 890)]
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
            hotelCategory: sector == TravelSector.Hotel ? HotelCategory.TwoStar : null));

        Assert.Equal(expectedRate, quote.Lines.Single().DiscountRate);
        Assert.Equal(expectedTotal, quote.Total);
    }

    [Fact]
    public void Calculate_AppliesCustomerDiscountAboveCombinedSubtotalThreshold()
    {
        var service = CreateService(7_550m);

        var quote = service.Calculate(CreateBooking(2, [TravelSector.Flight], customerCode: "ABC333"));

        Assert.Equal(15_100m, quote.Subtotal);
        Assert.Equal(2_038.50m, quote.DiscountAmount);
        Assert.Equal(13_061.50m, quote.Total);
    }

    [Fact]
    public void Calculate_DoesNotApplyCustomerDiscountAtThreshold()
    {
        var service = CreateService(7_500m);

        var quote = service.Calculate(CreateBooking(2, [TravelSector.Flight], customerCode: "ABC333"));

        Assert.Equal(15_000m, quote.Subtotal);
        Assert.Equal(0m, quote.DiscountAmount);
        Assert.Equal(15_000m, quote.Total);
    }

    [Fact]
    public void Calculate_CorporateDiscountTakesPrecedenceOverCustomerDiscount()
    {
        var service = CreateService(10_000m);

        var quote = service.Calculate(CreateBooking(1, [TravelSector.Flight], "111ZAA", "ABC333"));

        Assert.Equal(1_000m, quote.DiscountAmount);
        Assert.Equal(9_000m, quote.Total);
        Assert.Contains("not stacked", quote.DiscountDescription);
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
        var traveler = new Traveler("Amina", "Ndlovu");
        var service = new TravelQuoteService();

        var quote = service.Calculate(new TravelBooking(
            [traveler],
            [TravelSector.Hotel],
            HotelCategory.FiveStar,
            "111ZAA",
            null));

        Assert.Equal(9_000m, quote.Lines.Single().UnitPrice);
        Assert.Equal(8_010m, quote.Total);
        Assert.Equal([traveler], quote.Travelers);
    }

    [Fact]
    public void Calculate_RequiresTravelerFirstAndLastNames()
    {
        var service = CreateService(1_000m);
        var booking = new TravelBooking(
            [new Traveler("Amina", "")],
            [TravelSector.Flight],
            null,
            null,
            null);

        Assert.Throws<ArgumentException>(() => service.Calculate(booking));
    }

    [Fact]
    public void Calculate_RequiresHotelCategoryWhenHotelIsSelected()
    {
        var service = CreateService(1_000m);
        var booking = CreateBooking(1, [TravelSector.Hotel]);

        Assert.Throws<ArgumentException>(() => service.Calculate(booking));
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

    [Fact]
    public void Calculate_RejectsInvalidCorporateCode()
    {
        var service = CreateService(1_000m);

        Assert.Throws<ArgumentException>(() =>
            service.Calculate(CreateBooking(1, [TravelSector.Flight], "111XYZ")));
    }

    private static TravelBooking CreateBooking(
        int travelerCount,
        IReadOnlyList<TravelSector> sectors,
        string? corporateCode = null,
        string? customerCode = null,
        HotelCategory? hotelCategory = null) =>
        new(
            Enumerable.Range(1, travelerCount)
                .Select(index => new Traveler($"Test{index}", $"Traveler{index}"))
                .ToArray(),
            sectors,
            hotelCategory,
            corporateCode,
            customerCode);

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
