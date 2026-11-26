namespace CsharpDiscountCalculator.PhaseB.Models;

public sealed record Traveler(
    string FirstName,
    string LastName,
    string? CorporateCode = null,
    string? CustomerCode = null,
    TravelerTravelPreferences? TravelPreferences = null,
    string? FrequentFlyerCode = null);
