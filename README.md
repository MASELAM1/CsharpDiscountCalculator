# C# Discount Calculator

A complete console app that accepts prices, calculates discounts, and displays the
results sorted by discounted price.

## Discount rules

- Prices above R20 and below R50 receive 10% off.
- Prices at or above R50 receive 25% off.
- Prices at or below R20 receive no discount.

## Run

```bash
dotnet run --project CsharpDiscountCalculator.csproj
```

The app asks whether to enter prices through an array or a linked list.
Example interactive input:

```text
12.30, 33.89, 56.10, 9, 67.50
```

Command-line input is also supported:

```bash
dotnet run --project CsharpDiscountCalculator.csproj -- 12.30 33.89 56.10
```

## Test

```bash
dotnet test CsharpDiscountCalculator.slnx
```
