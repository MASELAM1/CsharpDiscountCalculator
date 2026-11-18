using System.Globalization;

namespace CsharpDiscountCalculator;

public static class ConsoleUI
{
    public static void Run(string[] args)
    {
        Console.WriteLine("================================");
        Console.WriteLine("  Discount Calculator");
        Console.WriteLine("================================");
        Console.WriteLine("Rules: > R20 = 10% off; >= R50 = 25% off");
        Console.WriteLine();

        if (args.Length > 0)
        {
            RunArgs(args);
            return;
        }

        while (true)
        {
            Console.WriteLine("Choose price collection:");
            Console.WriteLine("  1. Array");
            Console.WriteLine("  2. Linked list");
            Console.WriteLine("  3. Exit");
            Console.Write("Selection: ");

            var selection = Console.ReadLine()?.Trim();

            if (selection is "3" or "exit")
            {
                return;
            }

            if (selection is not ("1" or "2"))
            {
                Console.WriteLine("Please select 1, 2, or 3.");
                Console.WriteLine();
                continue;
            }

            Console.Write("Enter prices separated by commas or spaces: ");
            var input = Console.ReadLine();

            try
            {
                var prices = DiscountCalculator.ParsePrices(input ?? string.Empty);
                DisplayResults(selection == "1" ? "Array" : "Linked list", prices);
            }
            catch (Exception exception) when (
                exception is ArgumentException
                or FormatException
                or ArgumentOutOfRangeException)
            {
                Console.WriteLine($"Error: {exception.Message}");
            }

            Console.WriteLine();
            Console.Write("Calculate another set? (y/n): ");
            if (!Console.ReadLine()?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) ?? true)
            {
                return;
            }

            Console.WriteLine();
        }
    }

    private static void RunArgs(string[] args)
    {
        try
        {
            var prices = DiscountCalculator.ParsePrices(string.Join(" ", args));
            DisplayResults("Command line", prices);
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or FormatException
            or ArgumentOutOfRangeException)
        {
            Console.Error.WriteLine($"Error: {exception.Message}");
            Environment.ExitCode = 1;
        }
    }

    private static void DisplayResults(string collectionName, IReadOnlyList<decimal> prices)
    {
        Console.WriteLine();
        Console.WriteLine($"Using {collectionName}");

        IReadOnlyList<DiscountResult> results;
        var calculator = new DiscountCalculator();

        if (collectionName == "Linked list")
        {
            var linkedList = new LinkedList<decimal>();
            foreach (var price in prices)
            {
                linkedList.AddLast(price);
            }

            results = calculator.Calculate(linkedList);
        }
        else
        {
            results = calculator.Calculate(prices);
        }

        Console.WriteLine($"{"Original",-12}{"Discount",-12}{"Discounted",-12}");
        Console.WriteLine(new string('-', 36));

        foreach (var result in results)
        {
            Console.WriteLine(
                $"R{result.OriginalPrice.ToString("0.00", CultureInfo.InvariantCulture),-11}" +
                $"R{result.DiscountAmount.ToString("0.00", CultureInfo.InvariantCulture),-11}" +
                $"R{result.DiscountedPrice.ToString("0.00", CultureInfo.InvariantCulture)}");
        }
    }
}
