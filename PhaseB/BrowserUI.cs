using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using CsharpDiscountCalculator.PhaseB.Models;
using CsharpDiscountCalculator.PhaseB.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CsharpDiscountCalculator.PhaseB;

public static class BrowserUI
{
    public static async Task RunAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        await using var app = builder.Build();
        app.MapGet("/", async (HttpResponse response) =>
        {
            response.ContentType = "text/html; charset=utf-8";
            await response.WriteAsync(RenderPage());
        });

        app.MapPost("/", async (HttpRequest request, HttpResponse response) =>
        {
            var form = await request.ReadFormAsync();
            var firstNames = form["firstNames"].ToArray();
            var lastNames = form["lastNames"].ToArray();
            var corporateCodes = form["corporateCodes"].ToArray();
            var customerCodes = form["customerCodes"].ToArray();
            var travelers = new List<Traveler>();
            var hotelCategoryInput = form["hotelCategory"].ToString();
            var originAirportCodeInput = form["originAirportCode"].ToString();
            var destinationAirportCodeInput = form["destinationAirportCode"].ToString();
            var departureDateTimeInput = form["departureDateTime"].ToString();
            var arrivalDateTimeInput = form["arrivalDateTime"].ToString();
            var selectedSectors = new List<TravelSector>();
            HotelCategory? hotelCategory = null;
            TravelItinerary? itinerary = null;
            TravelQuote? quote = null;
            string? error = null;

            var travelerFieldCount = new[]
            {
                firstNames.Length,
                lastNames.Length,
                corporateCodes.Length,
                customerCodes.Length
            }.Max();
            for (var index = 0; index < travelerFieldCount; index++)
            {
                var firstName = index < firstNames.Length ? firstNames[index] ?? string.Empty : string.Empty;
                var lastName = index < lastNames.Length ? lastNames[index] ?? string.Empty : string.Empty;
                var corporateCode = index < corporateCodes.Length ? corporateCodes[index] ?? string.Empty : string.Empty;
                var customerCode = index < customerCodes.Length ? customerCodes[index] ?? string.Empty : string.Empty;
                travelers.Add(new Traveler(firstName, lastName, corporateCode, customerCode));
            }

            try
            {
                if (firstNames.Length != lastNames.Length)
                {
                    throw new ArgumentException("Enter a first and last name for every traveler.");
                }

                if (!DateTime.TryParseExact(
                    departureDateTimeInput,
                    "yyyy-MM-dd'T'HH:mm",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var departureDateTime))
                {
                    throw new FormatException("Enter a valid departure date and time.");
                }

                if (!DateTime.TryParseExact(
                    arrivalDateTimeInput,
                    "yyyy-MM-dd'T'HH:mm",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var arrivalDateTime))
                {
                    throw new FormatException("Enter a valid arrival date and time.");
                }

                itinerary = new TravelItinerary(
                    originAirportCodeInput,
                    destinationAirportCodeInput,
                    departureDateTime,
                    arrivalDateTime);

                foreach (var sectorValue in form["sectors"])
                {
                    if (!Enum.TryParse<TravelSector>(sectorValue, true, out var sector) || !Enum.IsDefined(sector))
                    {
                        throw new FormatException($"'{sectorValue}' is not a supported travel sector.");
                    }

                    selectedSectors.Add(sector);
                }

                if (!string.IsNullOrWhiteSpace(hotelCategoryInput))
                {
                    if (!Enum.TryParse<HotelCategory>(hotelCategoryInput, true, out var parsedCategory)
                        || !Enum.IsDefined(parsedCategory))
                    {
                        throw new FormatException("Choose BnB or a Hotel rating from 1 to 5 stars.");
                    }

                    hotelCategory = parsedCategory;
                }

                quote = new TravelQuoteService().Calculate(
                    new TravelBooking(travelers, selectedSectors, hotelCategory, itinerary));
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                error = exception.Message;
            }

            response.ContentType = "text/html; charset=utf-8";
            await response.WriteAsync(RenderPage(
                travelers,
                selectedSectors,
                hotelCategoryInput,
                quote,
                error,
                originAirportCodeInput,
                destinationAirportCodeInput,
                departureDateTimeInput,
                arrivalDateTimeInput));
        });

        await app.StartAsync();
        var address = app.Services
            .GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.Single();

        Console.WriteLine($"PhaseB browser interface is available at {address}");
        Console.WriteLine("Press Ctrl+C to stop the server.");

        try
        {
            Process.Start(new ProcessStartInfo(address) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Console.WriteLine($"Could not open a browser automatically: {exception.Message}");
            Console.WriteLine($"Open {address} in your browser.");
        }

        await app.WaitForShutdownAsync();
    }

    private static string RenderPage(
        IReadOnlyList<Traveler>? travelers = null,
        IReadOnlyCollection<TravelSector>? selectedSectors = null,
        string hotelCategoryInput = "",
        TravelQuote? quote = null,
        string? error = null,
        string originAirportCodeInput = "",
        string destinationAirportCodeInput = "",
        string departureDateTimeInput = "",
        string arrivalDateTimeInput = "")
    {
        var catalog = new TravelPriceCatalog();
        var formTravelers = travelers ?? [new Traveler(string.Empty, string.Empty)];
        var hotelSelected = selectedSectors?.Contains(TravelSector.Hotel) == true;
        var html = new StringBuilder("""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>PhaseB Travel Quote</title>
              <style>
                body { font-family: system-ui, sans-serif; max-width: 900px; margin: 2rem auto; padding: 0 1rem; color: #222; }
                h1 { margin-bottom: .4rem; }
                .hint { color: #555; }
                form, .quote { background: #f7f8fa; border: 1px solid #d5d9df; border-radius: 8px; padding: 1.2rem; }
                label { display: block; font-weight: 600; margin: 1rem 0 .4rem; }
                input, select, button { font: inherit; padding: .6rem; }
                input[type=text], select { box-sizing: border-box; max-width: 28rem; width: 100%; }
                .sector { display: inline-block; margin: .5rem 1rem .5rem 0; }
                .sector label { display: inline; font-weight: 400; }
                .traveler { border: 1px solid #d5d9df; border-radius: 4px; margin: .8rem 0; }
                button { cursor: pointer; margin-top: .5rem; }
                .error { background: #fff0f0; border: 1px solid #c62828; border-left: 5px solid #a00000; border-radius: 4px; color: #7a0000; font-weight: 600; margin: 1rem 0; padding: .8rem 1rem; }
                table { border-collapse: collapse; margin-top: 1rem; width: 100%; }
                th, td { border-bottom: 1px solid #ccc; padding: .65rem; text-align: right; }
                th:first-child, td:first-child { text-align: left; }
                .totals { margin-top: 1rem; text-align: right; }
                .grand-total { font-size: 1.3rem; font-weight: 700; }
                @media (max-width: 640px) { table { font-size: .85rem; } th, td { padding: .4rem .2rem; } }
              </style>
            </head>
            <body>
              <h1>Travel booking Quote <small>PhaseB</small></h1>
              <p class="hint">Demo prices are per traveler. BnB costs the same as a 2-star Hotel; higher star ratings cost more.</p>
              <form method="post">
                <fieldset>
                  <legend>Travelers</legend>
                  <div id="traveler-fields">
            """);

        for (var index = 0; index < formTravelers.Count; index++)
        {
            var traveler = formTravelers[index];
            html.Append("<fieldset class=\"traveler\"><legend>Traveler ")
                .Append(index + 1)
                .Append("</legend><label for=\"first-name-")
                .Append(index)
                .Append("\">First name</label><input id=\"first-name-")
                .Append(index)
                .Append("\" name=\"firstNames\" type=\"text\" autocomplete=\"given-name\" required value=\"")
                .Append(HtmlEncoder.Default.Encode(traveler.FirstName))
                .Append("\"><label for=\"last-name-")
                .Append(index)
                .Append("\">Last name</label><input id=\"last-name-")
                .Append(index)
                .Append("\" name=\"lastNames\" type=\"text\" autocomplete=\"family-name\" required value=\"")
                .Append(HtmlEncoder.Default.Encode(traveler.LastName))
                .Append("\"><label for=\"corporate-code-")
                .Append(index)
                .Append("\">Corporate code</label><input id=\"corporate-code-")
                .Append(index)
                .Append("\" name=\"corporateCodes\" type=\"text\" autocomplete=\"off\" value=\"")
                .Append(HtmlEncoder.Default.Encode(traveler.CorporateCode ?? string.Empty))
                .Append("\" placeholder=\"111ZAA\"><label for=\"customer-code-")
                .Append(index)
                .Append("\">Customer code</label><input id=\"customer-code-")
                .Append(index)
                .Append("\" name=\"customerCodes\" type=\"text\" autocomplete=\"off\" value=\"")
                .Append(HtmlEncoder.Default.Encode(traveler.CustomerCode ?? string.Empty))
                .Append("\" placeholder=\"ABC333\"><button type=\"button\" class=\"remove-traveler\">Remove traveler</button></fieldset>");
        }

        html.Append("<fieldset><legend>Flight itinerary</legend><p class=\"hint\">Classification uses the countries of the selected airports. Enter local date and time for each airport.</p>");
        AppendAirportSelect(html, "originAirportCode", "Origin airport", originAirportCodeInput);
        AppendAirportSelect(html, "destinationAirportCode", "Destination airport", destinationAirportCodeInput);
        html.Append("<label for=\"departure-date-time\">Departure date and local time</label><input id=\"departure-date-time\" name=\"departureDateTime\" type=\"datetime-local\" required value=\"")
            .Append(HtmlEncoder.Default.Encode(departureDateTimeInput))
            .Append("\"><label for=\"arrival-date-time\">Arrival date and local time</label><input id=\"arrival-date-time\" name=\"arrivalDateTime\" type=\"datetime-local\" required value=\"")
            .Append(HtmlEncoder.Default.Encode(arrivalDateTimeInput))
            .Append("\"></fieldset>");

        html.Append("""
                  </div>
                  <button type="button" id="add-traveler">Add traveler</button>
                </fieldset>
                <fieldset>
                  <legend>Select travel sectors</legend>
            """);

        foreach (var sector in Enum.GetValues<TravelSector>())
        {
            var checkedAttribute = selectedSectors?.Contains(sector) == true ? " checked" : string.Empty;
            html.Append("<div class=\"sector\"><input type=\"checkbox\" id=\"sector-")
                .Append(sector)
                .Append("\" name=\"sectors\" value=\"")
                .Append(sector)
                .Append('"')
                .Append(checkedAttribute)
                .Append("><label for=\"sector-")
                .Append(sector)
                .Append("\">")
                .Append(sector == TravelSector.Hotel ? "Hotel" : sector.ToString())
                .Append(sector == TravelSector.Hotel ? " (choose a category below)" : " — R")
                .Append(sector == TravelSector.Hotel ? string.Empty : FormatPrice(catalog.GetPricePerTraveler(sector)) + " per traveler")
                .Append("</label></div>");
        }

        html.Append("</fieldset><label for=\"hotel-category\">Hotel category</label>")
            .Append("<select id=\"hotel-category\" name=\"hotelCategory\"")
            .Append(hotelSelected ? " required" : " disabled")
            .Append("><option value=\"\">Choose a category</option>");

        foreach (var category in Enum.GetValues<HotelCategory>())
        {
            var selectedAttribute = string.Equals(category.ToString(), hotelCategoryInput, StringComparison.OrdinalIgnoreCase)
                ? " selected"
                : string.Empty;
            html.Append("<option value=\"")
                .Append(category)
                .Append('"')
                .Append(selectedAttribute)
                .Append('>')
                .Append(HtmlEncoder.Default.Encode(category.ToDisplayName()))
                .Append(" — R")
                .Append(FormatPrice(catalog.GetHotelPricePerTraveler(category)))
                .Append(" per traveler</option>");
        }

        html.Append("""
                </select>
                <p class="hint">Hotel prices are demo prices per traveler and differ by category. Enter a corporate code, a customer code, or neither for each traveler. Customer discounts require that traveler’s subtotal to be above R15,000; corporate discounts take precedence and do not stack.</p>
                <button type="submit">Calculate quote</button>
              </form>
            """);

        if (error is not null)
        {
            html.Append("<p class=\"error\" role=\"alert\">Error: ")
                .Append(HtmlEncoder.Default.Encode(error))
                .Append("</p>");
        }

        if (quote is not null)
        {
            html.Append("<section class=\"quote\" aria-labelledby=\"quote-title\"><h2 id=\"quote-title\">Booking quote</h2><p>Travelers: ")
                .Append(HtmlEncoder.Default.Encode(string.Join(", ", quote.Travelers.Select(
                    traveler => $"{traveler.FirstName} {traveler.LastName}"))))
                .Append("</p><p>Classification: ")
                .Append(HtmlEncoder.Default.Encode(quote.Classification.ToString()))
                .Append("</p><p>Route: ")
                .Append(HtmlEncoder.Default.Encode(
                    $"{AirportCodeCatalog.GetAirport(quote.Itinerary.OriginAirportCode).Name} ({quote.Itinerary.OriginAirportCode}) to {AirportCodeCatalog.GetAirport(quote.Itinerary.DestinationAirportCode).Name} ({quote.Itinerary.DestinationAirportCode})"))
                .Append("</p><p>Departure: ")
                .Append(FormatDateTime(quote.Itinerary.DepartureLocalDateTime))
                .Append(" (local)</p><p>Arrival: ")
                .Append(FormatDateTime(quote.Itinerary.ArrivalLocalDateTime))
                .Append("""
                </p>
                <table>
                  <thead><tr><th>Traveler</th><th>Sector</th><th>Unit price</th><th>Subtotal</th><th>Discount</th><th>Total</th></tr></thead>
                  <tbody>
            """);

            foreach (var line in quote.Lines)
            {
                var sectorName = line.Sector == TravelSector.Hotel
                    ? $"Hotel ({line.HotelCategory!.Value.ToDisplayName()})"
                    : line.Sector.ToString();
                html.Append("<tr><td>")
                    .Append(HtmlEncoder.Default.Encode($"{line.Traveler.FirstName} {line.Traveler.LastName}"))
                    .Append("</td><td>").Append(HtmlEncoder.Default.Encode(sectorName))
                    .Append("</td><td>R").Append(FormatPrice(line.UnitPrice))
                    .Append("</td><td>R").Append(FormatPrice(line.Subtotal))
                    .Append("</td><td>R").Append(FormatPrice(line.DiscountAmount))
                    .Append("</td><td>R").Append(FormatPrice(line.Total))
                    .Append("</td></tr>");
            }

            html.Append("</tbody></table><div class=\"totals\"><p>Subtotal: R")
                .Append(FormatPrice(quote.Subtotal))
                .Append("</p><p>Discount: R")
                .Append(FormatPrice(quote.DiscountAmount))
                .Append("</p><p class=\"grand-total\">Total: R")
                .Append(FormatPrice(quote.Total))
                .Append("</p><p>")
                .Append(HtmlEncoder.Default.Encode(quote.DiscountDescription))
                .Append("</p></div></section>");
        }

        html.Append("""
              <script>
                (() => {
                  const container = document.getElementById('traveler-fields');
                  let nextIndex = container.querySelectorAll('.traveler').length;
                  const addButton = document.getElementById('add-traveler');
                  const hotelCheckbox = document.getElementById('sector-Hotel');
                  const hotelCategory = document.getElementById('hotel-category');

                  addButton.addEventListener('click', () => {
                    const index = nextIndex++;
                    const row = document.createElement('fieldset');
                    row.className = 'traveler';
                    row.innerHTML =
                      '<legend>Traveler ' + (index + 1) + '</legend>' +
                      '<label for="first-name-' + index + '">First name</label>' +
                      '<input id="first-name-' + index + '" name="firstNames" type="text" autocomplete="given-name" required>' +
                      '<label for="last-name-' + index + '">Last name</label>' +
                      '<input id="last-name-' + index + '" name="lastNames" type="text" autocomplete="family-name" required>' +
                      '<label for="corporate-code-' + index + '">Corporate code</label>' +
                      '<input id="corporate-code-' + index + '" name="corporateCodes" type="text" autocomplete="off" placeholder="111ZAA">' +
                      '<label for="customer-code-' + index + '">Customer code</label>' +
                      '<input id="customer-code-' + index + '" name="customerCodes" type="text" autocomplete="off" placeholder="ABC333">' +
                      '<button type="button" class="remove-traveler">Remove traveler</button>';
                    container.appendChild(row);
                  });

                  container.addEventListener('click', event => {
                    if (event.target.closest('.remove-traveler')) {
                      event.target.closest('.traveler').remove();
                    }
                  });

                  function updateHotelCategory() {
                    hotelCategory.disabled = !hotelCheckbox.checked;
                    hotelCategory.required = hotelCheckbox.checked;
                  }

                  hotelCheckbox.addEventListener('change', updateHotelCategory);
                  updateHotelCategory();
                })();
              </script>
            </body>
            </html>
            """);

        return html.ToString();
    }

    private static void AppendAirportSelect(
        StringBuilder html,
        string fieldName,
        string label,
        string selectedCode)
    {
        html.Append("<label for=\"")
            .Append(fieldName)
            .Append("\">")
            .Append(label)
            .Append("</label><select id=\"")
            .Append(fieldName)
            .Append("\" name=\"")
            .Append(fieldName)
            .Append("\" required><option value=\"\">Choose an airport</option>");

        foreach (var airport in AirportCodeCatalog.SupportedAirports)
        {
            html.Append("<option value=\"")
                .Append(airport.Code)
                .Append('"')
                .Append(string.Equals(airport.Code, selectedCode, StringComparison.OrdinalIgnoreCase) ? " selected" : string.Empty)
                .Append('>')
                .Append(HtmlEncoder.Default.Encode($"{airport.Code} — {airport.Name}, {airport.CountryName}"))
                .Append("</option>");
        }

        html.Append("</select>");
    }

    private static string FormatPrice(decimal price) =>
        price.ToString("0.00", CultureInfo.InvariantCulture);

    private static string FormatDateTime(DateTime dateTime) =>
        dateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
