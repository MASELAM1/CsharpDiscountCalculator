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

## Code workflow

The application starts in `Program.cs`, which calls `ConsoleUI.Run(args)`.

1. **Input mode**
   - When prices are supplied as command-line arguments, `ConsoleUI` joins them
     into one string and processes them immediately.
   - Without command-line arguments, `ConsoleUI` presents an interactive menu.
     The user chooses an array, a linked list, or exit mode and enters prices
     separated by commas or spaces.

2. **Parsing**
   - `DiscountCalculator.ParsePrices` splits the input into individual values,
     parses them using invariant (non-region-specific) number formatting, and
     rounds each price to two decimal places.
   - The supported entry formats include comma-separated and space-separated
     prices, for example `12.30,33.89 56.10`.

3. **Calculation**
   - The array or linked-list collection is passed to `DiscountCalculator`.
   - The linked-list overload converts the linked list to an enumerable and
     uses the same calculation and sorting logic as the array overload.
   - `Calculate(decimal)` applies the discount rules and creates a
     `DiscountResult` containing the original price, discounted price, and
     discount amount.

4. **Sorting and output**
   - The collection overloads sort results by discounted price in ascending
     order, then by original price as a tie-breaker.
   - `ConsoleUI.DisplayResults` prints the original price, discount amount,
     and final discounted price in a table.

The main workflow is:

```text
Program
  -> ConsoleUI (interactive or command-line input)
  -> ParsePrices
  -> DiscountCalculator
  -> sort by discounted price
  -> ConsoleUI table output
```

## Exception handling

The application validates input before calculations and reports expected
input errors without allowing the console app to fail unexpectedly.

- `ArgumentException` is thrown when no price is entered.
- `FormatException` is thrown when a price cannot be parsed. The exception
  message identifies the invalid value.
- `ArgumentOutOfRangeException` is thrown when a price is negative. This check
  applies both during parsing and when calling
  `DiscountCalculator.Calculate(decimal)`.
- `ArgumentNullException` is thrown if an overload receives a `null` collection.

In interactive mode, `ConsoleUI` catches `ArgumentException`,
`FormatException`, and `ArgumentOutOfRangeException`, prints the error message,
and allows the user to try again. In command-line mode, it writes the error to
standard error and sets the process exit code to `1`. Unexpected exceptions are
not caught, so they remain visible for debugging rather than being presented as
normal input errors.

## Test

```bash
dotnet test CsharpDiscountCalculator.slnx
```
