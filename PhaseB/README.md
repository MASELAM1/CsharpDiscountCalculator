# PhaseB: Travel Sector Discounts

PhaseB is a separate version of the calculator. The original discount calculator
remains at the repository root; this phase adds named travelers, selected
sectors, hotel categories, demo prices, and corporate, customer, and
frequent-flyer discount codes.

## Run

```bash
dotnet run --project PhaseB/CsharpDiscountCalculator.PhaseB.csproj
```

PhaseB runs in the browser. The browser server listens on `127.0.0.1` using an
available port and prints its URL if it cannot open a browser automatically.
You can optionally pass `--browser` explicitly:

```bash
dotnet run --project PhaseB/CsharpDiscountCalculator.PhaseB.csproj -- --browser
```

## Itinerary classification

Select sectors separately for each traveler. The Flight sector displays that
traveler's itinerary fields for origin, destination, and departure and arrival
date/time; other travelers can select different sectors and enter different
routes and dates. A flight itinerary is required only for travelers who select
Flight. The displayed times are local times at their respective airports; they
are retained for the quote and do not affect classification.

- A route within one country is **Domestic**.
- A route between two SADC countries is **Regional**.
- Any other cross-border route is **International**.
- A route with a domestic first leg and an international second leg is
  **Domestic + International**.

Choose a trip type in the browser; the selected type is validated against the
route, rather than overriding its classification. The browser filters airport
choices to match the selected type. For **Domestic +
International**, choose an origin, a domestic connection in the same country,
and an international destination.

The browser offers South African airports first, followed by the other
supported airport codes. Unsupported codes are rejected rather than classified
by guesswork. A static list of South African domestic airport routes can fill
the browser's airport selectors. It is not a live schedule or a guarantee of
current airline service.
The airport-to-country mapping is maintained in
`Services/AirportCodeCatalog.cs`, and domestic route pairs are maintained in
`Services/DomesticFlightRouteCatalog.cs`.
The browser displays route-country flags beside the trip type and quote
classification. Flag images load from FlagCDN and require an internet connection.
For each traveler selecting Flight, the browser can include a return flight on
the reverse route. Enter its departure and arrival local times; the return leg
is priced as another one-way fare, with the domestic departure-time surcharge
applied independently to each leg. For domestic round trips, choose the
outbound and return flight companies separately; each leg is priced using its
own company and departure time.

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
more than the previous one. Choose a meal option for the hotel: Breakfast +
Dinner, Breakfast only, or Dinner only. The selected meal price is added to the
hotel category price:

| Meal option | Additional price per traveler |
| --- | ---: |
| Breakfast + Dinner | R210.00 |
| Breakfast only | R110.00 |
| Dinner only | R155.00 |

Other sector prices are:

| Sector | Price per traveler |
| --- | ---: |
| Car | R1,800.00 |
| Parking | R300.00 |

Flight prices vary by trip type and, for domestic flights, by company:

| Trip type | Price per traveler |
| --- | ---: |
| Domestic — SAA | R3,500.00 |
| Domestic — FlySafair | R4,000.00 |
| Domestic — Airlink | R4,790.00 |
| Regional | R6,000.00 |
| International | R7,500.00 |
| Domestic + International | R12,000.00 |

Domestic carrier fares are R120.00 higher for departures at or after 12:00
local time. Choose a carrier for each domestic leg. A return flight is priced
as an additional one-way fare and can use a different carrier from the
outbound flight.

## Discount rules

- Corporate code `111ZAA` applies sector discounts: Flight 10%, Hotel 11%,
  Car 8%, and Parking 6.7%.
- Discount eligibility is calculated separately for each traveler.
- Each traveler can enter a unique frequent-flyer code in the format
  `ZAA` + six digits + `FR` (for example, `ZAA989343FR`). It gives 12% off
  domestic flight lines and 20% off regional or international flight lines.
  Domestic + International itineraries are not included in these flight
  classification rates.
- A customer code must contain three letters followed by `333` (for example,
  `ABC333`). A valid customer code gives that traveler 13.5% off their
  pre-discount subtotal only when it is **above** R15,000. Exactly R15,000
  does not qualify.
- Codes are evaluated per traveler in this order: corporate, frequent-flyer,
  then customer. Discounts are not stacked. The frequent-flyer discount applies
  only to eligible flight lines; other travelers' codes and discounts are
  calculated independently.
- All amounts are rounded to two decimal places. The quote shows each
  traveler's selected sectors and discounts separately, then sums them into
  the booking totals.

Run the PhaseB tests with:

```bash
dotnet test PhaseB/tests/CsharpDiscountCalculator.PhaseB.Tests.csproj
```
