# PhaseB: Travel Sector Discounts

PhaseB is a separate version of the calculator. The original discount calculator
remains at the repository root; this phase adds named travelers, selected
sectors, hotel categories, demo prices, and corporate/customer discount codes.

## Run

```bash
dotnet run --project PhaseB/CsharpDiscountCalculator.PhaseB.csproj
```

Choose the terminal or browser interface. The browser server listens on
`127.0.0.1` using an available port and prints its URL if it cannot open a
browser automatically. You can also launch an interface directly:

```bash
dotnet run --project PhaseB/CsharpDiscountCalculator.PhaseB.csproj -- --browser
dotnet run --project PhaseB/CsharpDiscountCalculator.PhaseB.csproj -- --terminal
```

## Demo prices

Prices are demo amounts per traveler and are defined in
`Services/TravelPriceCatalog.cs`. Add each traveler using their first and last
name. If Hotel is selected, choose one hotel category:

| Hotel category | Price per traveler |
| --- | ---: |
| BnB | R4,200.00 |
| 1-star hotel | R3,000.00 |
| 2-star hotel | R4,200.00 |
| 3-star hotel | R5,400.00 |
| 4-star hotel | R7,000.00 |
| 5-star hotel | R9,000.00 |

BnB is priced the same as a 2-star hotel, and each higher star level costs
more than the previous one. Other sector prices are:

| Sector | Price per traveler |
| --- | ---: |
| Car | R1,800.00 |
| Flight | R7,500.00 |
| Parking | R300.00 |

## Discount rules

- Corporate code `111ZAA` applies sector discounts: Flight 10%, Hotel 11%,
  Car 8%, and Parking 6.7%.
- A customer code must contain three letters followed by `333` (for example,
  `ABC333`). A valid customer code gives 13.5% off the combined booking
  subtotal only when that subtotal is **above** R15,000.
- The customer threshold uses the pre-discount subtotal. Exactly R15,000 does
  not qualify.
- Corporate discounts take precedence if both valid codes are entered; the
  discounts are not stacked.
- All amounts are rounded to two decimal places. Corporate discounts are
  calculated per selected sector; customer discounts are calculated once for
  the combined booking subtotal.

Run the PhaseB tests with:

```bash
dotnet test PhaseB/tests/CsharpDiscountCalculator.PhaseB.Tests.csproj
```
