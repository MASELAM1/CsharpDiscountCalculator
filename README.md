# C# Discount Calculator

A discount calculator with terminal and browser interfaces. Both accept prices,
apply the same rules, and display results sorted by discounted price.

## Discount rules

- Prices above R20 and below R50 receive 10% off.
- Prices at or above R50 receive 25% off.
- Prices at or below R20 receive no discount.

## Run

```bash
dotnet run --project CsharpDiscountCalculator.csproj
```

Choose **Terminal** to use the existing interactive terminal interface, or
**Browser** to start a local web interface and open it in your default browser.
If the browser does not open automatically, the app prints the local URL to
open manually. Stop the browser server with Ctrl+C.

In either interface, enter values separated by commas or spaces and choose an
array or linked list. Example input:

```text
12.30, 33.89, 56.10, 9, 67.50
```

Command-line input is also supported and continues to use the terminal output:

```bash
dotnet run --project CsharpDiscountCalculator.csproj -- 12.30 33.89 56.10
```

## Code workflow

The application starts in `Program.cs`, which calls `ConsoleUI.RunAsync(args)`.

1. **Input mode**
   - When prices are supplied as command-line arguments, `ConsoleUI` joins them
     into one string and processes them through the terminal interface.
   - Without command-line arguments, the user chooses the terminal interface,
     browser interface, or exit.
   - The terminal interface preserves the array/linked-list selection and
     prompts for another calculation. The browser interface accepts input on a
     local web page and displays results there.

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
   - The terminal interface prints the original price, discount amount, and
     final discounted price in a table. The browser interface renders the same
     columns and order in an HTML table.

The main workflow is:

```text
Program
  -> ConsoleUI (terminal, browser, or command-line mode)
  -> ParsePrices
  -> DiscountCalculator
  -> sort by discounted price
  -> terminal or browser table output
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

In terminal interactive mode, `ConsoleUI` catches input exceptions, prints the
error message, and allows the user to try again. In browser mode, the page
displays the validation error and keeps the entered prices available for
correction. In command-line mode, errors are written to standard error and the
process exit code is set to `1`. Unexpected exceptions are not caught as input
errors, so they remain visible for debugging.

## Test

```bash
dotnet test CsharpDiscountCalculator.slnx
```
