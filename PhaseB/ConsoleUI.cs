using System.Globalization;
using CsharpDiscountCalculator.PhaseB.Models;
using CsharpDiscountCalculator.PhaseB.Services;

namespace CsharpDiscountCalculator.PhaseB;

public static class ConsoleUI
{
    public static async Task RunAsync(string[] args)
    {
        if (args.Length > 0)
        {
            if (args.Length == 1 && args[0] == "--browser")
            {
                await BrowserUI.RunAsync();
                return;
            }

            if (args.Length == 1 && args[0] == "--terminal")
            {
                RunTerminal();
                return;
            }

            Console.Error.WriteLine("Usage: dotnet run --project PhaseB/CsharpDiscountCalculator.PhaseB.csproj [--browser|--terminal]");
            Environment.ExitCode = 1;
            return;
        }

        while (true)
        {
            Console.WriteLine("================================");
            Console.WriteLine("  Travel Discount Calculator");
            Console.WriteLine("================================");
            Console.WriteLine("  1. Terminal");
            Console.WriteLine("  2. Browser");
            Console.WriteLine("  3. Exit");
            Console.Write("Selection: ");

            switch (Console.ReadLine()?.Trim())
            {
                case "1":
                    RunTerminal();
                    return;
                case "2":
                    await BrowserUI.RunAsync();
                    return;
                case "3":
                case "exit":
                    return;
                default:
                    Console.WriteLine("Please select 1, 2, or 3.");
                    Console.WriteLine();
                    break;
            }
        }
    }

    private static void RunTerminal()
    {
        var quoteService = new TravelQuoteService();

        while (true)
        {
            Console.Write("Number of travelers: ");
            var travelerInput = Console.ReadLine();

            try
            {
                if (!int.TryParse(travelerInput, NumberStyles.None, CultureInfo.InvariantCulture, out var travelerCount))
                {
                    throw new FormatException("Enter a whole number for the traveler count.");
                }

                if (travelerCount <= 0)
                {
                    throw new ArgumentException("Traveler count must be greater than zero.");
                }

                var travelers = new List<Traveler>(travelerCount);
                for (var index = 0; index < travelerCount; index++)
                {
                    Console.Write($"Traveler {index + 1} first name: ");
                    var firstName = Console.ReadLine() ?? string.Empty;
                    Console.Write($"Traveler {index + 1} last name: ");
                    var lastName = Console.ReadLine() ?? string.Empty;
                    Console.Write($"Traveler {index + 1} corporate code (optional, 111ZAA): ");
                    var corporateCode = Console.ReadLine();
                    Console.Write($"Traveler {index + 1} customer code (optional, three letters followed by 333): ");
                    var customerCode = Console.ReadLine();
                    travelers.Add(new Traveler(firstName, lastName, corporateCode, customerCode));
                }

                Console.Write("Origin airport code: ");
                var originAirportCode = Console.ReadLine() ?? string.Empty;
                Console.Write("Destination airport code: ");
                var destinationAirportCode = Console.ReadLine() ?? string.Empty;
                Console.Write("Departure date and local time (yyyy-MM-dd HH:mm): ");
                var departureDateTime = ParseDateTime(Console.ReadLine(), "departure");
                Console.Write("Arrival date and local time (yyyy-MM-dd HH:mm): ");
                var arrivalDateTime = ParseDateTime(Console.ReadLine(), "arrival");
                var itinerary = new TravelItinerary(
                    originAirportCode,
                    destinationAirportCode,
                    departureDateTime,
                    arrivalDateTime);

                Console.Write("Sectors (Hotel, Car, Flight, Parking; comma-separated): ");
                var sectors = ParseSectors(Console.ReadLine());
                HotelCategory? hotelCategory = null;
                if (sectors.Contains(TravelSector.Hotel))
                {
                    Console.Write("Hotel category (BnB or 1-5 stars): ");
                    hotelCategory = ParseHotelCategory(Console.ReadLine());
                }

                var quote = quoteService.Calculate(
                    new TravelBooking(travelers, sectors, hotelCategory, itinerary));
                DisplayQuote(quote);
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                Console.WriteLine($"Error: {exception.Message}");
            }

            Console.WriteLine();
            Console.Write("Create another quote? (y/n): ");
            if (!Console.ReadLine()?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) ?? true)
            {
                return;
            }

            Console.WriteLine();
        }
    }

    private static IReadOnlyList<TravelSector> ParseSectors(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return [];
        }

        var sectors = new List<TravelSector>();
        foreach (var value in input.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Enum.TryParse<TravelSector>(value, true, out var sector) || !Enum.IsDefined(sector))
            {
                throw new FormatException($"'{value}' is not a supported travel sector.");
            }

            sectors.Add(sector);
        }

        return sectors;
    }

    private static HotelCategory ParseHotelCategory(string? input) =>
        input?.Trim().ToUpperInvariant() switch
        {
            "BNB" or "B&B" => HotelCategory.BedAndBreakfast,
            "1" or "1 STAR" => HotelCategory.OneStar,
            "2" or "2 STAR" => HotelCategory.TwoStar,
            "3" or "3 STAR" => HotelCategory.ThreeStar,
            "4" or "4 STAR" => HotelCategory.FourStar,
            "5" or "5 STAR" => HotelCategory.FiveStar,
            _ => throw new FormatException("Choose BnB or a hotel rating from 1 to 5 stars.")
        };

    private static DateTime ParseDateTime(string? input, string label)
    {
        if (DateTime.TryParseExact(
            input,
            "yyyy-MM-dd HH:mm",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var dateTime))
        {
            return dateTime;
        }

        throw new FormatException($"Enter a valid {label} date and time in yyyy-MM-dd HH:mm format.");
    }

    private static void DisplayQuote(TravelQuote quote)
    {
        Console.WriteLine();
        Console.WriteLine("Travelers: " + string.Join(", ", quote.Travelers.Select(
            traveler => $"{traveler.FirstName} {traveler.LastName}")));
        var origin = AirportCodeCatalog.GetAirport(quote.Itinerary.OriginAirportCode);
        var destination = AirportCodeCatalog.GetAirport(quote.Itinerary.DestinationAirportCode);
        Console.WriteLine($"Classification: {quote.Classification}");
        Console.WriteLine($"Route: {origin.Name} ({origin.Code}) to {destination.Name} ({destination.Code})");
        Console.WriteLine($"Departure: {FormatDateTime(quote.Itinerary.DepartureLocalDateTime)} (local)");
        Console.WriteLine($"Arrival: {FormatDateTime(quote.Itinerary.ArrivalLocalDateTime)} (local)");
        Console.WriteLine($"{"Traveler",-24}{"Sector",-20}{"Unit price",-14}{"Subtotal",-14}{"Discount",-14}{"Total",-14}");
        Console.WriteLine(new string('-', 100));

        foreach (var line in quote.Lines)
        {
            var sectorName = line.Sector == TravelSector.Hotel
                ? $"Hotel ({line.HotelCategory!.Value.ToDisplayName()})"
                : line.Sector.ToString();
            var travelerName = $"{line.Traveler.FirstName} {line.Traveler.LastName}";
            Console.WriteLine(
                $"{travelerName,-24}{sectorName,-20}" +
                $"R{FormatPrice(line.UnitPrice),-13}" +
                $"R{FormatPrice(line.Subtotal),-13}" +
                $"R{FormatPrice(line.DiscountAmount),-13}" +
                $"R{FormatPrice(line.Total),-13}");
        }

        Console.WriteLine($"Subtotal: R{FormatPrice(quote.Subtotal)}");
        Console.WriteLine($"Discount: R{FormatPrice(quote.DiscountAmount)}");
        Console.WriteLine($"Total:    R{FormatPrice(quote.Total)}");
        Console.WriteLine(quote.DiscountDescription);
    }

    private static string FormatPrice(decimal price) =>
        price.ToString("0.00", CultureInfo.InvariantCulture);

    private static string FormatDateTime(DateTime dateTime) =>
        dateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
