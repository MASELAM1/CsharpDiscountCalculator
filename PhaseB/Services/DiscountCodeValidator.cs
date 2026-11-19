using System.Text.RegularExpressions;

namespace CsharpDiscountCalculator.PhaseB.Services;

public static partial class DiscountCodeValidator
{
    public static bool IsCorporateCode(string? code) =>
        string.Equals(code?.Trim(), "111ZAA", StringComparison.OrdinalIgnoreCase);

    public static bool IsCustomerCode(string? code) =>
        !string.IsNullOrWhiteSpace(code) && CustomerCodePattern().IsMatch(code.Trim());

    [GeneratedRegex(@"\A[A-Za-z]{3}333\z", RegexOptions.CultureInvariant)]
    private static partial Regex CustomerCodePattern();
}
