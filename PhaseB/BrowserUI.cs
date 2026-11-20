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
            var travelClassificationInput = form["travelClassification"].ToString();
            var originAirportCodeInput = form["originAirportCode"].ToString();
            var connectionAirportCodeInput = form["connectionAirportCode"].ToString();
            var destinationAirportCodeInput = form["destinationAirportCode"].ToString();
            var departureDateTimeInput = form["departureDateTime"].ToString();
            var arrivalDateTimeInput = form["arrivalDateTime"].ToString();
            var returnFlightInput = string.Equals(form["returnFlight"], "true", StringComparison.OrdinalIgnoreCase);
            var returnDepartureDateTimeInput = form["returnDepartureDateTime"].ToString();
            var returnArrivalDateTimeInput = form["returnArrivalDateTime"].ToString();
            var flightCompanyInput = form["flightCompany"].ToString();
            var selectedSectors = new List<TravelSector>();
            HotelCategory? hotelCategory = null;
            FlightCompany? flightCompany = null;
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

                DateTime? returnDepartureDateTime = null;
                DateTime? returnArrivalDateTime = null;
                if (returnFlightInput)
                {
                    if (!DateTime.TryParseExact(
                        returnDepartureDateTimeInput,
                        "yyyy-MM-dd'T'HH:mm",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var parsedReturnDepartureDateTime))
                    {
                        throw new FormatException("Enter a valid return departure date and time.");
                    }

                    if (!DateTime.TryParseExact(
                        returnArrivalDateTimeInput,
                        "yyyy-MM-dd'T'HH:mm",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var parsedReturnArrivalDateTime))
                    {
                        throw new FormatException("Enter a valid return arrival date and time.");
                    }

                    returnDepartureDateTime = parsedReturnDepartureDateTime;
                    returnArrivalDateTime = parsedReturnArrivalDateTime;
                }

                if (!Enum.TryParse<TravelClassification>(
                    travelClassificationInput,
                    true,
                    out var selectedClassification)
                    || !Enum.IsDefined(selectedClassification))
                {
                    throw new FormatException("Choose a supported trip type.");
                }

                itinerary = new TravelItinerary(
                    originAirportCodeInput,
                    destinationAirportCodeInput,
                    departureDateTime,
                    arrivalDateTime,
                    connectionAirportCodeInput,
                    returnDepartureDateTime,
                    returnArrivalDateTime);
                TravelClassificationService.ValidateSelection(selectedClassification, itinerary);

                foreach (var sectorValue in form["sectors"])
                {
                    if (!Enum.TryParse<TravelSector>(sectorValue, true, out var sector) || !Enum.IsDefined(sector))
                    {
                        throw new FormatException($"'{sectorValue}' is not a supported travel sector.");
                    }

                    selectedSectors.Add(sector);
                }

                if (!string.IsNullOrWhiteSpace(flightCompanyInput))
                {
                    if (!Enum.TryParse<FlightCompany>(flightCompanyInput, true, out var parsedFlightCompany)
                        || !Enum.IsDefined(parsedFlightCompany))
                    {
                        throw new FormatException("Choose SAA, FlySafair, or Airlink for a domestic flight.");
                    }

                    flightCompany = parsedFlightCompany;
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
                    new TravelBooking(travelers, selectedSectors, hotelCategory, itinerary, flightCompany));
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
                travelClassificationInput,
                originAirportCodeInput,
                connectionAirportCodeInput,
                destinationAirportCodeInput,
                departureDateTimeInput,
                arrivalDateTimeInput,
                flightCompanyInput,
                returnFlightInput,
                returnDepartureDateTimeInput,
                returnArrivalDateTimeInput));
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
        string travelClassificationInput = nameof(TravelClassification.Domestic),
        string originAirportCodeInput = "",
        string connectionAirportCodeInput = "",
        string destinationAirportCodeInput = "",
        string departureDateTimeInput = "",
        string arrivalDateTimeInput = "",
        string flightCompanyInput = "",
        bool returnFlightInput = false,
        string returnDepartureDateTimeInput = "",
        string returnArrivalDateTimeInput = "")
    {
        var catalog = new TravelPriceCatalog();
        var formTravelers = travelers ?? [new Traveler(string.Empty, string.Empty)];
        var hotelSelected = selectedSectors?.Contains(TravelSector.Hotel) == true;
        var flightSelected = selectedSectors?.Contains(TravelSector.Flight) == true;
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
                .classification-label { align-items: center; display: flex; gap: .65rem; margin: 1rem 0 .4rem; }
                .classification-label label { margin: 0; }
                .classification-flags { align-items: center; display: inline-flex; gap: .35rem; }
                .country-flag { border: 1px solid #d5d9df; border-radius: 2px; height: 20px; object-fit: cover; width: 30px; }
                .sector .return-flight-option { align-items: center; display: flex; gap: .5rem; }
                .sector .return-flight-option input { width: auto; }
                .sector .return-flight-times label { display: block; }
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
                .sector-quote { margin-top: 1.25rem; }
                .sector-quote h3 { font-size: 1.05rem; margin: .5rem 0 0; }
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

        html.Append("<fieldset><legend>Flight itinerary</legend><p class=\"hint\">Choose a trip type to filter the airport selectors. Domestic + International represents a domestic leg followed by an international leg. Route options are a static list and do not provide live schedules.</p>");
        AppendTravelClassificationSelect(html, travelClassificationInput);
        html.Append("<div id=\"domestic-route-field\">");
        AppendDomesticRouteSelect(html, originAirportCodeInput, destinationAirportCodeInput);
        html.Append("</div>");
        AppendAirportSelect(html, "originAirportCode", "Origin airport", originAirportCodeInput);
        html.Append("<div id=\"connection-airport-field\">");
        AppendAirportSelect(html, "connectionAirportCode", "Domestic connection airport", connectionAirportCodeInput);
        html.Append("</div>");
        AppendAirportSelect(html, "destinationAirportCode", "Destination airport", destinationAirportCodeInput);
        html.Append("<label for=\"departure-date-time\">Departure date and local time</label><input id=\"departure-date-time\" name=\"departureDateTime\" type=\"datetime-local\" required value=\"")
            .Append(HtmlEncoder.Default.Encode(departureDateTimeInput))
            .Append("\"><label for=\"arrival-date-time\">Arrival date and local time</label><input id=\"arrival-date-time\" name=\"arrivalDateTime\" type=\"datetime-local\" required value=\"")
            .Append(HtmlEncoder.Default.Encode(arrivalDateTimeInput))
            .Append("\">");
        AppendFlightCompanySelect(
            html,
            flightCompanyInput,
            travelClassificationInput.Equals(nameof(TravelClassification.Domestic), StringComparison.OrdinalIgnoreCase),
            flightSelected);
        html.Append("</fieldset>");

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
            var priceDescription = sector switch
            {
                TravelSector.Hotel => " (choose a category below)",
                TravelSector.Flight =>
                    " — Domestic SAA R3,500.00; FlySafair R4,000.00; Airlink R4,790.00"
                    + " (+R120.00 for departures at/after 12:00)"
                    + "; Regional R" + FormatPrice(catalog.GetPricePerTraveler(
                        sector,
                        travelClassification: TravelClassification.Regional))
                    + "; International R" + FormatPrice(catalog.GetPricePerTraveler(
                        sector,
                        travelClassification: TravelClassification.International))
                    + "; Domestic + International R" + FormatPrice(catalog.GetPricePerTraveler(
                        sector,
                        travelClassification: TravelClassification.DomesticAndInternational))
                    + " per traveler",
                _ => " — R" + FormatPrice(catalog.GetPricePerTraveler(sector)) + " per traveler"
            };
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
                .Append(priceDescription)
                .Append("</label>");
            if (sector == TravelSector.Flight)
            {
                AppendReturnFlightControls(
                    html,
                    flightSelected,
                    returnFlightInput,
                    returnDepartureDateTimeInput,
                    returnArrivalDateTimeInput);
            }

            html.Append("</div>");
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
                .Append(HtmlEncoder.Default.Encode(quote.Classification.ToDisplayName()));
            AppendCountryFlags(html, quote.Itinerary);
            html.Append("</p>");
            if (quote.FlightCompany is { } quoteFlightCompany)
            {
                html.Append("<p>Flight company: ")
                    .Append(HtmlEncoder.Default.Encode(quoteFlightCompany.ToString()))
                    .Append("</p>");
            }

            html.Append("<p>Route: ")
                .Append(HtmlEncoder.Default.Encode(FormatRoute(quote.Itinerary)))
                .Append("</p><p>Departure: ")
                .Append(FormatDateTime(quote.Itinerary.DepartureLocalDateTime))
                .Append(" (local)</p><p>Arrival: ")
                .Append(FormatDateTime(quote.Itinerary.ArrivalLocalDateTime))
                .Append(" (local)</p>");

            if (quote.Itinerary.ReturnDepartureLocalDateTime is { } returnDeparture
                && quote.Itinerary.ReturnArrivalLocalDateTime is { } returnArrival)
            {
                html.Append("<p>Return route: ")
                    .Append(HtmlEncoder.Default.Encode(FormatReturnRoute(quote.Itinerary)))
                    .Append("</p><p>Return departure: ")
                    .Append(FormatDateTime(returnDeparture))
                    .Append(" (local)</p><p>Return arrival: ")
                    .Append(FormatDateTime(returnArrival))
                    .Append(" (local)</p>");
            }

            foreach (var sectorLines in quote.Lines.GroupBy(line => line.Sector))
            {
                var firstLine = sectorLines.First();
                var sectorName = firstLine.Sector == TravelSector.Hotel
                    ? $"Hotel ({firstLine.HotelCategory!.Value.ToDisplayName()})"
                    : firstLine.Sector == TravelSector.Flight
                        && quote.Itinerary.ReturnDepartureLocalDateTime is not null
                            ? "Flight (round trip)"
                            : firstLine.Sector.ToString();
                html.Append("<section class=\"sector-quote\"><h3>")
                    .Append(HtmlEncoder.Default.Encode(sectorName))
                    .Append("""
                    </h3><table>
                      <thead><tr><th>Traveler</th><th>Unit price</th><th>Subtotal</th><th>Discount</th><th>Total</th></tr></thead>
                      <tbody>
                    """);

                foreach (var line in sectorLines)
                {
                    html.Append("<tr><td>")
                        .Append(HtmlEncoder.Default.Encode($"{line.Traveler.FirstName} {line.Traveler.LastName}"))
                        .Append("</td><td>R").Append(FormatPrice(line.UnitPrice))
                        .Append("</td><td>R").Append(FormatPrice(line.Subtotal))
                        .Append("</td><td>R").Append(FormatPrice(line.DiscountAmount))
                        .Append("</td><td>R").Append(FormatPrice(line.Total))
                        .Append("</td></tr>");
                }

                html.Append("</tbody></table></section>");
            }

            html.Append("<div class=\"totals\"><p>Subtotal: R")
                .Append(FormatPrice(quote.Subtotal))
                .Append("</p><p>Discount: R")
                .Append(FormatPrice(quote.DiscountAmount))
                .Append("</p><p>")
                .Append(HtmlEncoder.Default.Encode(quote.DiscountDescription))
                .Append("</p><p class=\"grand-total\">Booking total: R")
                .Append(FormatPrice(quote.Total))
                .Append("</p></div></section>");
        }

        html.Append("""
              <script>
                (() => {
                  const container = document.getElementById('traveler-fields');
                  let nextIndex = container.querySelectorAll('.traveler').length;
                  const addButton = document.getElementById('add-traveler');
                  const hotelCheckbox = document.getElementById('sector-Hotel');
                  const flightCheckbox = document.getElementById('sector-Flight');
                  const hotelCategory = document.getElementById('hotel-category');
                  const flightCompanyField = document.getElementById('flight-company-field');
                  const flightCompany = document.getElementById('flightCompany');
                  const travelClassification = document.getElementById('travelClassification');
                  const domesticRouteField = document.getElementById('domestic-route-field');
                  const connectionAirportField = document.getElementById('connection-airport-field');
                  const domesticRoute = document.getElementById('domestic-route');
                  const originAirport = document.getElementById('originAirportCode');
                  const connectionAirport = document.getElementById('connectionAirportCode');
                  const destinationAirport = document.getElementById('destinationAirportCode');
                  const classificationFlags = document.getElementById('classification-flags');
                  const returnFlightField = document.getElementById('return-flight-field');
                  const returnFlight = document.getElementById('returnFlight');
                  const returnFlightTimes = document.getElementById('return-flight-times');
                  const returnDeparture = document.getElementById('return-departure-date-time');
                  const returnArrival = document.getElementById('return-arrival-date-time');

                  function updateClassificationFlags() {
                    classificationFlags.replaceChildren();
                    const countries = [originAirport.selectedOptions[0], destinationAirport.selectedOptions[0]]
                      .filter(option => option?.value)
                      .map(option => ({ code: option.dataset.country, name: option.dataset.countryName }))
                      .filter((country, index, values) =>
                        values.findIndex(value => value.code === country.code) === index);

                    for (const country of countries) {
                      const flag = document.createElement('img');
                      flag.className = 'country-flag';
                      flag.src = 'https://flagcdn.com/w40/' + country.code.toLowerCase() + '.png';
                      flag.alt = country.name + ' flag';
                      flag.title = country.name;
                      flag.width = 30;
                      flag.height = 20;
                      classificationFlags.appendChild(flag);
                    }
                  }

                  function updateAirportOptions() {
                    const selectedType = travelClassification.value;
                    const isCombined = selectedType === 'DomesticAndInternational';
                    domesticRouteField.hidden = selectedType !== 'Domestic';
                    connectionAirportField.hidden = !isCombined;
                    connectionAirport.disabled = !isCombined;
                    connectionAirport.required = isCombined;

                    for (const option of originAirport.options) {
                      option.hidden = Boolean(option.value)
                        && selectedType === 'Regional'
                        && option.dataset.regional !== 'true';
                    }
                    if (originAirport.selectedOptions[0]?.hidden) originAirport.value = '';

                    const originOption = originAirport.selectedOptions[0];
                    const originCountry = originOption?.dataset.country;
                    for (const option of connectionAirport.options) {
                      option.hidden = Boolean(option.value)
                        && (!isCombined || !originCountry || option.dataset.country !== originCountry
                          || option.value === originAirport.value);
                    }
                    if (connectionAirport.selectedOptions[0]?.hidden) connectionAirport.value = '';

                    const connectionOption = connectionAirport.selectedOptions[0];
                    const baseOption = isCombined ? connectionOption : originOption;
                    for (const option of destinationAirport.options) {
                      if (!option.value || !baseOption) {
                        option.hidden = false;
                        continue;
                      }

                      const sameCountry = option.dataset.country === baseOption.dataset.country;
                      const bothRegional = option.dataset.regional === 'true'
                        && baseOption.dataset.regional === 'true';
                      option.hidden = selectedType === 'Domestic'
                        ? !sameCountry || option.value === originAirport.value
                        : selectedType === 'Regional'
                          ? !bothRegional || sameCountry
                          : sameCountry || bothRegional;
                    }
                    if (destinationAirport.selectedOptions[0]?.hidden) destinationAirport.value = '';
                    updateClassificationFlags();
                  }

                  function updateFlightCompany() {
                    const needed = travelClassification.value === 'Domestic' && flightCheckbox.checked;
                    flightCompanyField.hidden = !needed;
                    flightCompany.disabled = !needed;
                    flightCompany.required = needed;
                  }

                  function updateReturnFlight() {
                    const flightSelected = flightCheckbox.checked;
                    const returnSelected = flightSelected && returnFlight.checked;
                    returnFlightField.hidden = !flightSelected;
                    returnFlight.disabled = !flightSelected;
                    returnFlightTimes.hidden = !returnSelected;
                    returnDeparture.disabled = !returnSelected;
                    returnDeparture.required = returnSelected;
                    returnArrival.disabled = !returnSelected;
                    returnArrival.required = returnSelected;
                  }

                  travelClassification.addEventListener('change', updateAirportOptions);
                  originAirport.addEventListener('change', updateAirportOptions);
                  connectionAirport.addEventListener('change', updateAirportOptions);
                  destinationAirport.addEventListener('change', updateAirportOptions);
                  travelClassification.addEventListener('change', updateFlightCompany);
                  flightCheckbox.addEventListener('change', updateFlightCompany);
                  flightCheckbox.addEventListener('change', updateReturnFlight);
                  returnFlight.addEventListener('change', updateReturnFlight);

                  domesticRoute.addEventListener('change', () => {
                    if (!domesticRoute.value) return;
                    const [origin, destination] = domesticRoute.value.split('-');
                    originAirport.value = origin;
                    destinationAirport.value = destination;
                    updateAirportOptions();
                    updateFlightCompany();
                  });
                  updateAirportOptions();
                  updateReturnFlight();

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
                .Append("\" data-country=\"")
                .Append(airport.CountryCode)
                .Append("\" data-country-name=\"")
                .Append(HtmlEncoder.Default.Encode(airport.CountryName))
                .Append("\" data-regional=\"")
                .Append(TravelClassificationService.IsRegionalCountry(airport.CountryCode) ? "true" : "false")
                .Append('"')
                .Append(string.Equals(airport.Code, selectedCode, StringComparison.OrdinalIgnoreCase) ? " selected" : string.Empty)
                .Append('>')
                .Append(HtmlEncoder.Default.Encode($"{airport.Code} — {airport.Name}, {airport.CountryName}"))
                .Append("</option>");
        }

        html.Append("</select>");
    }

    private static void AppendTravelClassificationSelect(StringBuilder html, string selectedClassification)
    {
        html.Append("<div class=\"classification-label\"><label for=\"travelClassification\">Trip type</label>")
            .Append("<span id=\"classification-flags\" class=\"classification-flags\" aria-live=\"polite\"></span></div>")
            .Append("<select id=\"travelClassification\" name=\"travelClassification\" required>");

        foreach (var classification in Enum.GetValues<TravelClassification>())
        {
            html.Append("<option value=\"")
                .Append(classification)
                .Append('"')
                .Append(string.Equals(
                    classification.ToString(),
                    selectedClassification,
                    StringComparison.OrdinalIgnoreCase) ? " selected" : string.Empty)
                .Append('>')
                .Append(HtmlEncoder.Default.Encode(classification.ToDisplayName()))
                .Append("</option>");
        }

        html.Append("</select>");
    }

    private static void AppendCountryFlags(StringBuilder html, TravelItinerary itinerary)
    {
        var origin = AirportCodeCatalog.GetAirport(itinerary.OriginAirportCode);
        var destination = AirportCodeCatalog.GetAirport(itinerary.DestinationAirportCode);
        var countries = origin.CountryCode == destination.CountryCode
            ? [origin]
            : new[] { origin, destination };

        html.Append("<span class=\"classification-flags\" aria-label=\"Route country flags\">");
        foreach (var airport in countries)
        {
            html.Append("<img class=\"country-flag\" src=\"https://flagcdn.com/w40/")
                .Append(airport.CountryCode.ToLowerInvariant())
                .Append(".png\" width=\"30\" height=\"20\" alt=\"")
                .Append(HtmlEncoder.Default.Encode($"{airport.CountryName} flag"))
                .Append("\" title=\"")
                .Append(HtmlEncoder.Default.Encode(airport.CountryName))
                .Append("\">");
        }

        html.Append("</span>");
    }

    private static void AppendFlightCompanySelect(
        StringBuilder html,
        string selectedCompany,
        bool isDomestic,
        bool flightSelected)
    {
        var isEnabled = isDomestic && flightSelected;
        html.Append("<div id=\"flight-company-field\"")
            .Append(isEnabled ? string.Empty : " hidden")
            .Append("><label for=\"flightCompany\">Domestic flight company</label>")
            .Append("<select id=\"flightCompany\" name=\"flightCompany\"")
            .Append(isEnabled ? " required" : " disabled")
            .Append("><option value=\"\">Choose a company</option>");

        foreach (var company in Enum.GetValues<FlightCompany>())
        {
            html.Append("<option value=\"")
                .Append(company)
                .Append('"')
                .Append(string.Equals(company.ToString(), selectedCompany, StringComparison.OrdinalIgnoreCase)
                    ? " selected"
                    : string.Empty)
                .Append('>')
                .Append(company)
                .Append("</option>");
        }

        html.Append("</select></div>");
    }

    private static void AppendReturnFlightControls(
        StringBuilder html,
        bool flightSelected,
        bool returnFlightSelected,
        string returnDepartureDateTime,
        string returnArrivalDateTime)
    {
        html.Append("<div id=\"return-flight-field\"")
            .Append(flightSelected ? string.Empty : " hidden")
            .Append("><label class=\"return-flight-option\"><input id=\"returnFlight\" name=\"returnFlight\" type=\"checkbox\" value=\"true\"")
            .Append(returnFlightSelected ? " checked" : string.Empty)
            .Append(flightSelected ? string.Empty : " disabled")
            .Append(">Include return flight</label><div id=\"return-flight-times\"")
            .Append(flightSelected && returnFlightSelected ? string.Empty : " hidden")
            .Append("><label for=\"return-departure-date-time\">Return departure date and local time</label><input id=\"return-departure-date-time\" name=\"returnDepartureDateTime\" type=\"datetime-local\"")
            .Append(flightSelected && returnFlightSelected ? " required" : " disabled")
            .Append(" value=\"")
            .Append(HtmlEncoder.Default.Encode(returnDepartureDateTime))
            .Append("\"><label for=\"return-arrival-date-time\">Return arrival date and local time</label><input id=\"return-arrival-date-time\" name=\"returnArrivalDateTime\" type=\"datetime-local\"")
            .Append(flightSelected && returnFlightSelected ? " required" : " disabled")
            .Append(" value=\"")
            .Append(HtmlEncoder.Default.Encode(returnArrivalDateTime))
            .Append("\"></div></div>");
    }

    private static void AppendDomesticRouteSelect(
        StringBuilder html,
        string originAirportCode,
        string destinationAirportCode)
    {
        html.Append("<label for=\"domestic-route\">South African domestic route</label>")
            .Append("<select id=\"domestic-route\"><option value=\"\">Choose a domestic route</option>");

        foreach (var route in DomesticFlightRouteCatalog.SupportedRoutes)
        {
            var selectedAttribute = string.Equals(
                    route.OriginAirportCode,
                    originAirportCode,
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    route.DestinationAirportCode,
                    destinationAirportCode,
                    StringComparison.OrdinalIgnoreCase)
                ? " selected"
                : string.Empty;

            html.Append("<option value=\"")
                .Append(route.OriginAirportCode)
                .Append('-')
                .Append(route.DestinationAirportCode)
                .Append('"')
                .Append(selectedAttribute)
                .Append('>')
                .Append(HtmlEncoder.Default.Encode(route.DisplayName))
                .Append("</option>");
        }

        html.Append("</select>");
    }

    private static string FormatRoute(TravelItinerary itinerary)
    {
        static string FormatAirport(string code)
        {
            var airport = AirportCodeCatalog.GetAirport(code);
            return $"{airport.Name} ({airport.Code})";
        }

        var route = $"{FormatAirport(itinerary.OriginAirportCode)} to ";
        if (!string.IsNullOrWhiteSpace(itinerary.ConnectionAirportCode))
        {
            route += $"{FormatAirport(itinerary.ConnectionAirportCode)} to ";
        }

        return route + FormatAirport(itinerary.DestinationAirportCode);
    }

    private static string FormatReturnRoute(TravelItinerary itinerary)
    {
        static string FormatAirport(string code)
        {
            var airport = AirportCodeCatalog.GetAirport(code);
            return $"{airport.Name} ({airport.Code})";
        }

        var route = $"{FormatAirport(itinerary.DestinationAirportCode)} to ";
        if (!string.IsNullOrWhiteSpace(itinerary.ConnectionAirportCode))
        {
            route += $"{FormatAirport(itinerary.ConnectionAirportCode)} to ";
        }

        return route + FormatAirport(itinerary.OriginAirportCode);
    }

    private static string FormatPrice(decimal price) =>
        price.ToString("0.00", CultureInfo.InvariantCulture);

    private static string FormatDateTime(DateTime dateTime) =>
        dateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
