namespace CsharpDiscountCalculator.PhaseB.Models;

public enum TravelClassification
{
    Domestic,
    Regional,
    International,
    DomesticAndInternational
}

public static class TravelClassificationExtensions
{
    public static string ToDisplayName(this TravelClassification classification) =>
        classification switch
        {
            TravelClassification.DomesticAndInternational => "Domestic + International",
            _ => classification.ToString()
        };
}
