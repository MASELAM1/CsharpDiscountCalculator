using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using CsharpDiscountCalculator.PhaseB.Models;
using CsharpDiscountCalculator.PhaseB.Services;
using Microsoft.AspNetCore.Http;

namespace CsharpDiscountCalculator.PhaseB;

internal sealed record TravelerBookingFormResult(
    IReadOnlyList<Traveler> Travelers,
    TravelQuote? Quote,
    string? Error);

internal static class TravelerBookingFormPage
{
    public static TravelerBookingFormResult Calculate(IFormCollection form)
    {
        var firstNames = form["firstNames"].ToArray();
        var lastNames = form["lastNames"].ToArray();
        var corporateCodes = form["corporateCodes"].ToArray();
        var customerCodes = form["customerCodes"].ToArray();
        var travelerCount = new[]
        {
            firstNames.Length,
            lastNames.Length,
            corporateCodes.Length,
            customerCodes.Length
        }.Max();
        var travelers = Enumerable.Range(0, travelerCount)
            .Select(index => new Traveler(
                ValueAt(firstNames, index),
                ValueAt(lastNames, index),
                ValueAt(corporateCodes, index),
                ValueAt(customerCodes, index)))
            .ToArray();

        try
        {
            if (firstNames.Length != lastNames.Length
                || corporateCodes.Length != travelerCount
                || customerCodes.Length != travelerCount)
            {
                throw new ArgumentException("Enter a first and last name for every traveler.");
            }

            for (var index = 0; index < travelers.Length; index++)
            {
                travelers[index] = travelers[index] with
                {
                    TravelPreferences = ParsePreferences(form, index)
                };
            }

            var quote = new TravelQuoteService().Calculate(new TravelBooking(
                travelers,
                [],
                null,
                null));
            return new TravelerBookingFormResult(travelers, quote, null);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            return new TravelerBookingFormResult(travelers, null, exception.Message);
        }
    }

    public static string Render(
        IFormCollection? form = null,
        IReadOnlyList<Traveler>? travelers = null,
        TravelQuote? quote = null,
        string? error = null)
    {
        var formTravelers = travelers ?? [new Traveler(string.Empty, string.Empty)];
        var html = new StringBuilder("""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>PhaseB Travel Quote</title>
              <style>
                body { font-family: system-ui, sans-serif; max-width: 960px; margin: 2rem auto; padding: 0 1rem; color: #222; }
                .hint { color: #555; }
                form, .quote, .traveler { border: 1px solid #d5d9df; border-radius: 8px; padding: 1rem; }
                form, .quote { background: #f7f8fa; }
                .traveler { margin: 1rem 0; background: white; }
                label { display: block; font-weight: 600; margin: .8rem 0 .35rem; }
                input, select, button { font: inherit; padding: .55rem; }
                input[type=text], input[type=datetime-local], select { box-sizing: border-box; max-width: 28rem; width: 100%; }
                .sectors { display: flex; flex-wrap: wrap; gap: .5rem 1rem; }
                .sectors label, .return-option { display: inline-flex; align-items: center; gap: .4rem; font-weight: 400; }
                .sectors input, .return-option input { width: auto; }
                .classification-label { align-items: center; display: flex; gap: .6rem; }
                .classification-flags { display: inline-flex; gap: .3rem; }
                .country-flag { border: 1px solid #d5d9df; height: 20px; object-fit: cover; width: 30px; }
                .flight-details, .hotel-details { border: 1px solid #e1e5ea; margin-top: 1rem; padding: .8rem; }
                .meal-samples { display: flex; gap: .75rem; margin-top: .75rem; }
                .meal-samples figure { margin: 0; text-align: center; }
                .meal-samples img { background: #f7f8fa; border: 1px solid #d5d9df; border-radius: 6px; height: 56px; object-fit: contain; padding: 4px; width: 72px; }
                .meal-samples figcaption { color: #555; font-size: .8rem; }
                .error { background: #fff0f0; border-left: 5px solid #a00000; color: #7a0000; padding: .8rem 1rem; }
                .quote { background: #fff; margin-top: 2rem; }
                .traveler-quote { border-top: 1px solid #d5d9df; margin-top: 1.4rem; padding-top: 1rem; }
                .itinerary { background: #f7f8fa; padding: .8rem; }
                table { border-collapse: collapse; width: 100%; }
                th, td { border-bottom: 1px solid #ddd; padding: .55rem; text-align: right; }
                th:first-child, td:first-child { text-align: left; }
                .totals { margin: 1rem 0 0 auto; max-width: 25rem; }
                .total { display: flex; justify-content: space-between; padding: .35rem 0; }
              </style>
            </head>
            <body>
              <h1>Travel Booking Quote <small>PhaseB</small></h1>
              <p class="hint">Choose sectors separately for each traveler. A flight itinerary is required only for travelers who select Flight.</p>
              <form method="post">
                <div id="traveler-fields">
            """);

        for (var index = 0; index < formTravelers.Count; index++)
        {
            AppendTravelerFields(html, index.ToString(CultureInfo.InvariantCulture), formTravelers[index], form);
        }

        html.Append("""
                </div>
                <button type="button" id="add-traveler">Add traveler</button>
                <button type="submit">Calculate Quote</button>
              </form>
              <template id="traveler-template">
            """);
        AppendTravelerFields(
            html,
            "__INDEX__",
            new Traveler(string.Empty, string.Empty, TravelPreferences: new TravelerTravelPreferences([])));
        html.Append("""
              </template>
            """);

        if (error is not null)
        {
            html.Append("<p class=\"error\" role=\"alert\">Error: ")
                .Append(HtmlEncoder.Default.Encode(error))
                .Append("</p>");
        }

        if (quote is not null)
        {
            AppendQuote(html, quote);
        }

        html.Append("""
              <script>
                (() => {
                  const container = document.getElementById('traveler-fields');
                  const template = document.getElementById('traveler-template');
                  const updateTraveler = traveler => {
                    const hotelSelected = traveler.querySelector('[data-sector="Hotel"]').checked;
                    const hotelDetails = traveler.querySelector('.hotel-details');
                    hotelDetails.hidden = !hotelSelected;
                    for (const field of hotelDetails.querySelectorAll('select')) {
                      field.disabled = !hotelSelected;
                      field.required = hotelSelected;
                    }

                    const flightSelected = traveler.querySelector('[data-sector="Flight"]').checked;
                    const flightDetails = traveler.querySelector('.flight-details');
                    flightDetails.hidden = !flightSelected;
                    flightDetails.disabled = !flightSelected;
                    for (const field of flightDetails.querySelectorAll('[data-required]')) {
                      field.required = flightSelected;
                    }
                    const domestic = traveler.querySelector('[data-field="classification"]').value === 'Domestic';
                    updateAirportOptions(traveler, flightSelected);
                    const outboundCompany = traveler.querySelector('.outbound-company');
                    outboundCompany.hidden = !domestic;
                    outboundCompany.querySelector('select').required = flightSelected && domestic;

                    const returning = flightSelected && traveler.querySelector('[data-field="returnFlight"]').checked;
                    const returnDetails = traveler.querySelector('.return-details');
                    returnDetails.hidden = !returning;
                    for (const field of returnDetails.querySelectorAll('[data-required]')) {
                      field.required = returning;
                    }
                    const returnCompany = traveler.querySelector('.return-company');
                    returnCompany.hidden = !returning || !domestic;
                    returnCompany.querySelector('select').disabled = !returning || !domestic;
                  };

                  const updateAirportOptions = (traveler, flightSelected) => {
                    const selectedType = traveler.querySelector('[data-field="classification"]').value;
                    const isCombined = selectedType === 'DomesticAndInternational';
                    const domesticRouteField = traveler.querySelector('.domestic-route-field');
                    const connectionField = traveler.querySelector('.connection-field');
                    const origin = traveler.querySelector('[data-field="originAirport"]');
                    const connection = traveler.querySelector('[data-field="connectionAirport"]');
                    const destination = traveler.querySelector('[data-field="destinationAirport"]');
                    const domesticRoute = traveler.querySelector('[data-field="domesticRoute"]');
                    const flags = traveler.querySelector('.classification-flags');
                    domesticRouteField.hidden = selectedType !== 'Domestic';
                    connectionField.hidden = !isCombined;
                    connection.required = flightSelected && isCombined;

                    for (const option of origin.options) {
                      option.hidden = Boolean(option.value)
                        && selectedType === 'Regional'
                        && option.dataset.regional !== 'true';
                    }
                    if (origin.selectedOptions[0]?.hidden) origin.value = '';
                    const originCountry = origin.selectedOptions[0]?.dataset.country;
                    for (const option of connection.options) {
                      option.hidden = Boolean(option.value)
                        && (!isCombined || !originCountry || option.dataset.country !== originCountry
                          || option.value === origin.value);
                    }
                    if (connection.selectedOptions[0]?.hidden) connection.value = '';

                    const base = isCombined ? connection.selectedOptions[0] : origin.selectedOptions[0];
                    for (const option of destination.options) {
                      if (!option.value || !base) {
                        option.hidden = false;
                        continue;
                      }

                      const sameCountry = option.dataset.country === base.dataset.country;
                      const bothRegional = option.dataset.regional === 'true'
                        && base.dataset.regional === 'true';
                      option.hidden = selectedType === 'Domestic'
                        ? !sameCountry || option.value === origin.value
                        : selectedType === 'Regional'
                          ? !bothRegional || sameCountry
                          : sameCountry || bothRegional;
                    }
                    if (destination.selectedOptions[0]?.hidden) destination.value = '';
                    flags.replaceChildren();
                    const countries = [origin.selectedOptions[0], destination.selectedOptions[0]]
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
                      flags.appendChild(flag);
                    }
                    domesticRoute.value = Array.from(domesticRoute.options)
                      .find(option => option.value === origin.value + '-' + destination.value)?.value ?? '';
                  };

                  const reindexTravelers = () => {
                    for (const [index, traveler] of container.querySelectorAll('.traveler').entries()) {
                      const oldIndex = traveler.dataset.index;
                      const newIndex = String(index);
                      for (const field of traveler.querySelectorAll('[name]')) {
                        field.name = field.name.replace(new RegExp('_' + oldIndex + '$'), '_' + newIndex);
                      }
                      for (const element of traveler.querySelectorAll('[id], label[for]')) {
                        if (element.id) element.id = element.id.replace(new RegExp('-' + oldIndex + '$'), '-' + newIndex);
                        if (element.htmlFor) {
                          element.htmlFor = element.htmlFor.replace(new RegExp('-' + oldIndex + '$'), '-' + newIndex);
                        }
                      }
                      traveler.dataset.index = newIndex;
                      traveler.querySelector('legend').textContent = 'Traveler ' + (index + 1);
                    }
                  };

                  container.addEventListener('change', event => {
                    const traveler = event.target.closest('.traveler');
                    if (traveler) {
                      if (event.target.matches('[data-field="domesticRoute"]')) {
                        const [origin, destination] = event.target.value.split('-');
                        traveler.querySelector('[data-field="originAirport"]').value = origin ?? '';
                        traveler.querySelector('[data-field="destinationAirport"]').value = destination ?? '';
                      }
                      updateTraveler(traveler);
                    }
                  });
                  container.addEventListener('click', event => {
                    const button = event.target.closest('.remove-traveler');
                    if (button) {
                      button.closest('.traveler').remove();
                      reindexTravelers();
                    }
                  });
                  document.getElementById('add-traveler').addEventListener('click', () => {
                    const index = String(container.querySelectorAll('.traveler').length);
                    const fragment = document.createElement('template');
                    fragment.innerHTML = template.innerHTML.replaceAll('__INDEX__', index);
                    const traveler = fragment.content.firstElementChild;
                    container.appendChild(traveler);
                    reindexTravelers();
                    updateTraveler(traveler);
                  });
                  for (const traveler of container.querySelectorAll('.traveler')) updateTraveler(traveler);
                })();
              </script>
            </body>
            </html>
            """);
        return html.ToString();
    }

    private static TravelerTravelPreferences ParsePreferences(IFormCollection form, int index)
    {
        var sectors = new List<TravelSector>();
        foreach (var value in form[$"sectors_{index}"])
        {
            if (!Enum.TryParse<TravelSector>(value, true, out var sector) || !Enum.IsDefined(sector))
            {
                throw new FormatException($"'{value}' is not a supported travel sector for traveler {index + 1}.");
            }

            sectors.Add(sector);
        }

        var hotelCategory = ParseOptionalEnum<HotelCategory>(Value(form, $"hotelCategory_{index}"));
        var hotelMealOption = ParseOptionalEnum<HotelMealOption>(Value(form, $"hotelMealOption_{index}"));
        TravelItinerary? itinerary = null;
        FlightCompany? flightCompany = null;
        FlightCompany? returnFlightCompany = null;
        if (sectors.Contains(TravelSector.Flight))
        {
            var classificationInput = Value(form, $"travelClassification_{index}");
            if (!Enum.TryParse<TravelClassification>(classificationInput, true, out var selectedClassification)
                || !Enum.IsDefined(selectedClassification))
            {
                throw new FormatException($"Choose a supported trip type for traveler {index + 1}.");
            }

            itinerary = new TravelItinerary(
                Value(form, $"originAirportCode_{index}"),
                Value(form, $"destinationAirportCode_{index}"),
                ParseDateTime(form, $"departureDateTime_{index}", "departure", index),
                ParseDateTime(form, $"arrivalDateTime_{index}", "arrival", index),
                Value(form, $"connectionAirportCode_{index}"),
                IsChecked(form, $"returnFlight_{index}")
                    ? ParseDateTime(form, $"returnDepartureDateTime_{index}", "return departure", index)
                    : null,
                IsChecked(form, $"returnFlight_{index}")
                    ? ParseDateTime(form, $"returnArrivalDateTime_{index}", "return arrival", index)
                    : null);
            TravelClassificationService.ValidateSelection(selectedClassification, itinerary);
            if (selectedClassification == TravelClassification.Domestic)
            {
                flightCompany = ParseOptionalEnum<FlightCompany>(Value(form, $"flightCompany_{index}"));
                if (IsChecked(form, $"returnFlight_{index}"))
                {
                    returnFlightCompany =
                        ParseOptionalEnum<FlightCompany>(Value(form, $"returnFlightCompany_{index}"));
                }
            }
        }

        return new TravelerTravelPreferences(
            sectors,
            hotelCategory,
            hotelMealOption,
            itinerary,
            flightCompany,
            returnFlightCompany);
    }

    private static void AppendTravelerFields(
        StringBuilder html,
        string index,
        Traveler traveler,
        IFormCollection? form = null)
    {
        var preferences = traveler.TravelPreferences;
        var sectors = preferences?.SelectedSectors ?? [];
        var catalog = new TravelPriceCatalog();
        var firstName = Value(form, $"firstNames", traveler.FirstName, index);
        var lastName = Value(form, $"lastNames", traveler.LastName, index);
        html.Append("<fieldset class=\"traveler\" data-index=\"").Append(index).Append("\"><legend>Traveler ")
            .Append(int.TryParse(index, out var number) ? number + 1 : 1)
            .Append("</legend>")
            .Append("<label for=\"first-name-").Append(index).Append("\">First name</label>")
            .Append("<input id=\"first-name-").Append(index).Append("\" name=\"firstNames\" type=\"text\" required value=\"")
            .Append(Encode(firstName)).Append("\">")
            .Append("<label for=\"last-name-").Append(index).Append("\">Last name</label>")
            .Append("<input id=\"last-name-").Append(index).Append("\" name=\"lastNames\" type=\"text\" required value=\"")
            .Append(Encode(lastName)).Append("\">")
            .Append("<label for=\"corporate-code-").Append(index).Append("\">Corporate code</label>")
            .Append("<input id=\"corporate-code-").Append(index).Append("\" name=\"corporateCodes\" type=\"text\" value=\"")
            .Append(Encode(Value(form, "corporateCodes", traveler.CorporateCode ?? string.Empty, index)))
            .Append("\" placeholder=\"111ZAA\">")
            .Append("<label for=\"customer-code-").Append(index).Append("\">Customer code</label>")
            .Append("<input id=\"customer-code-").Append(index).Append("\" name=\"customerCodes\" type=\"text\" value=\"")
            .Append(Encode(Value(form, "customerCodes", traveler.CustomerCode ?? string.Empty, index)))
            .Append("\" placeholder=\"ABC333\"><p>Select sectors</p><div class=\"sectors\">");
        foreach (var sector in Enum.GetValues<TravelSector>())
        {
            var checkedValue = IsSectorSelected(form, index, sector, sectors) ? " checked" : string.Empty;
            html.Append("<label><input type=\"checkbox\" name=\"sectors_").Append(index)
                .Append("\" value=\"").Append(sector).Append("\" data-sector=\"").Append(sector).Append('"')
                .Append(checkedValue).Append('>').Append(sector == TravelSector.Hotel ? "Hotel" : sector.ToString())
                .Append(sector switch
                {
                    TravelSector.Hotel => " (choose a category)",
                    TravelSector.Flight => $" — Domestic SAA R3,500.00, FlySafair R4,000.00, Airlink R4,790.00"
                        + " (+R120.00 for departures at/after 12:00)"
                        + $"; Regional R{FormatPrice(catalog.GetPricePerTraveler(
                        sector,
                        travelClassification: TravelClassification.Regional))}; International R{FormatPrice(catalog.GetPricePerTraveler(
                        sector,
                        travelClassification: TravelClassification.International))}; Domestic + International R{FormatPrice(catalog.GetPricePerTraveler(
                        sector,
                        travelClassification: TravelClassification.DomesticAndInternational))}",
                    _ => $" — R{FormatPrice(catalog.GetPricePerTraveler(sector))} per traveler"
                })
                .Append("</label>");
        }

        html.Append("</div><fieldset class=\"hotel-details\"><legend>Hotel</legend>");
        AppendSelect<HotelCategory>(
            html,
            $"hotelCategory_{index}",
            $"hotel-category-{index}",
            "Hotel category",
            Value(form, $"hotelCategory_{index}", preferences?.HotelCategory?.ToString() ?? string.Empty),
            category => $"{category.ToDisplayName()} — R{FormatPrice(catalog.GetHotelPricePerTraveler(category))}");
        AppendSelect<HotelMealOption>(
            html,
            $"hotelMealOption_{index}",
            $"hotel-meal-{index}",
            "Meal option",
            Value(form, $"hotelMealOption_{index}", preferences?.HotelMealOption?.ToString() ?? string.Empty),
            option => $"{option.ToDisplayName()} — +R{FormatPrice(catalog.GetHotelMealPricePerTraveler(option))}");
        html.Append("""
            </fieldset>
            <div class="meal-samples" aria-label="Meal examples">
              <figure><img src="/images/breakfast.svg" alt="Sample breakfast with toast, egg, and fruit"><figcaption>Breakfast</figcaption></figure>
              <figure><img src="/images/dinner.svg" alt="Sample dinner with vegetables and a main dish"><figcaption>Dinner</figcaption></figure>
            </div>
            <fieldset class="flight-details"><legend>Flight itinerary</legend>
            <div class="classification-label">
            """);
        AppendSelect<TravelClassification>(
            html,
            $"travelClassification_{index}",
            $"classification-{index}",
            "Trip type",
            Value(form, $"travelClassification_{index}", nameof(TravelClassification.Domestic)),
            classification => classification.ToDisplayName(),
            required: true,
            dataField: "classification");
        html.Append("<span class=\"classification-flags\" aria-live=\"polite\"></span></div>")
            .Append("<div class=\"domestic-route-field\">");
        AppendDomesticRouteSelect(
            html,
            index,
            Value(form, $"originAirportCode_{index}"),
            Value(form, $"destinationAirportCode_{index}"));
        html.Append("</div>");
        AppendAirportSelect(
            html,
            index,
            "originAirportCode",
            "Origin airport",
            Value(form, $"originAirportCode_{index}"),
            dataField: "originAirport");
        html.Append("<div class=\"connection-field\">");
        AppendAirportSelect(
            html,
            index,
            "connectionAirportCode",
            "Connection airport (optional)",
            Value(form, $"connectionAirportCode_{index}"),
            required: false,
            dataField: "connectionAirport");
        html.Append("</div>");
        AppendAirportSelect(
            html,
            index,
            "destinationAirportCode",
            "Destination airport",
            Value(form, $"destinationAirportCode_{index}"),
            dataField: "destinationAirport");
        AppendDateTime(html, index, "departureDateTime", "Departure date and local time", form);
        AppendDateTime(html, index, "arrivalDateTime", "Arrival date and local time", form);
        html.Append("<div class=\"outbound-company\">");
        AppendSelect<FlightCompany>(
            html,
            $"flightCompany_{index}",
            $"flight-company-{index}",
            "Domestic outbound airline (required for domestic flights)",
            Value(form, $"flightCompany_{index}", preferences?.FlightCompany?.ToString() ?? string.Empty),
            required: true);
        html.Append("</div>");
        var returnChecked = IsChecked(form, $"returnFlight_{index}")
            || form is null && preferences?.Itinerary?.ReturnDepartureLocalDateTime is not null;
        html.Append("<label class=\"return-option\"><input type=\"checkbox\" name=\"returnFlight_").Append(index)
            .Append("\" value=\"true\" data-field=\"returnFlight\"")
            .Append(returnChecked ? " checked" : string.Empty)
            .Append(">Include return flight</label><div class=\"return-details\">");
        AppendDateTime(html, index, "returnDepartureDateTime", "Return departure date and local time", form);
        AppendDateTime(html, index, "returnArrivalDateTime", "Return arrival date and local time", form);
        html.Append("<div class=\"return-company\">");
        AppendSelect<FlightCompany>(
            html,
            $"returnFlightCompany_{index}",
            $"return-flight-company-{index}",
            "Domestic return airline (optional)",
            Value(form, $"returnFlightCompany_{index}", preferences?.ReturnFlightCompany?.ToString() ?? string.Empty));
        html.Append("</div></div></fieldset><button type=\"button\" class=\"remove-traveler\">Remove traveler</button></fieldset>");
    }

    private static void AppendCountryFlags(StringBuilder html, TravelItinerary itinerary)
    {
        var origin = AirportCodeCatalog.GetAirport(itinerary.OriginAirportCode);
        var destination = AirportCodeCatalog.GetAirport(itinerary.DestinationAirportCode);
        var routeAirports = origin.CountryCode == destination.CountryCode
            ? new[] { origin }
            : new[] { origin, destination };
        html.Append("<span class=\"classification-flags\" aria-label=\"Route country flags\">");
        foreach (var airport in routeAirports)
        {
            html.Append("<img class=\"country-flag\" src=\"https://flagcdn.com/w40/")
                .Append(airport.CountryCode.ToLowerInvariant())
                .Append(".png\" width=\"30\" height=\"20\" alt=\"")
                .Append(Encode($"{airport.CountryName} flag"))
                .Append("\" title=\"")
                .Append(Encode(airport.CountryName))
                .Append("\">");
        }

        html.Append("</span>");
    }

    private static void AppendQuote(StringBuilder html, TravelQuote quote)
    {
        html.Append("<section class=\"quote\"><h2>Booking Quote</h2><p>")
            .Append(quote.Travelers.Count)
            .Append(quote.Travelers.Count == 1 ? " traveler" : " travelers")
            .Append("</p>");
        foreach (var (traveler, index) in quote.Travelers.Select((traveler, index) => (traveler, index)))
        {
            var lines = quote.Lines.Where(line => line.Traveler == traveler).ToArray();
            html.Append("<section class=\"traveler-quote\"><h3>")
                .Append(index + 1).Append(". ")
                .Append(Encode($"{traveler.FirstName} {traveler.LastName}"))
                .Append("</h3>");
            var flight = lines.FirstOrDefault(line => line.Sector == TravelSector.Flight);
            if (flight?.Itinerary is { } itinerary)
            {
                html.Append("<div class=\"itinerary\"><strong>")
                    .Append(Encode(flight.Classification?.ToDisplayName() ?? string.Empty))
                    .Append("</strong>");
                AppendCountryFlags(html, itinerary);
                html.Append("<p>")
                    .Append(Encode(FormatRoute(itinerary)))
                    .Append(" — ")
                    .Append(FormatDateTime(itinerary.DepartureLocalDateTime))
                    .Append(" to ")
                    .Append(FormatDateTime(itinerary.ArrivalLocalDateTime))
                    .Append("</p>");
                if (flight.FlightCompany is { } outbound)
                {
                    html.Append("<p>Outbound airline: ").Append(Encode(outbound.ToString())).Append("</p>");
                }

                if (itinerary.ReturnDepartureLocalDateTime is { } returnDeparture
                    && itinerary.ReturnArrivalLocalDateTime is { } returnArrival)
                {
                    html.Append("<p>Return: ")
                        .Append(Encode(FormatReturnRoute(itinerary)))
                        .Append(" — ").Append(FormatDateTime(returnDeparture))
                        .Append(" to ").Append(FormatDateTime(returnArrival));
                    if (flight.ReturnFlightCompany is { } returnCompany)
                    {
                        html.Append("; airline: ").Append(Encode(returnCompany.ToString()));
                    }

                    html.Append("</p>");
                }

                html.Append("</div>");
            }

            html.Append("<div style=\"overflow-x:auto\"><table><thead><tr><th>Sector</th><th>Price</th><th>Savings</th><th>Amount due</th></tr></thead><tbody>");
            foreach (var line in lines)
            {
                var name = line.Sector == TravelSector.Hotel
                    ? $"Hotel ({line.HotelCategory?.ToDisplayName()}, {line.HotelMealOption?.ToDisplayName()})"
                    : line.Sector.ToString();
                html.Append("<tr><td>").Append(Encode(name)).Append("</td><td>R")
                    .Append(FormatPrice(line.Subtotal)).Append("</td><td>R")
                    .Append(FormatPrice(line.DiscountAmount)).Append("</td><td>R")
                    .Append(FormatPrice(line.Total)).Append("</td></tr>");
            }

            var travelerSubtotal = lines.Sum(line => line.Subtotal);
            var travelerDiscount = lines.Sum(line => line.DiscountAmount);
            var travelerTotal = lines.Sum(line => line.Total);
            html.Append("</tbody></table></div><div class=\"totals traveler-totals\"><div class=\"total\"><span>Subtotal</span><strong>R")
                .Append(FormatPrice(travelerSubtotal)).Append("</strong></div><div class=\"total\"><span>Savings</span><strong>R")
                .Append(FormatPrice(travelerDiscount)).Append("</strong></div><div class=\"total\"><span>Percentage saved/lost</span><strong>")
                .Append(FormatSavingsPercentage(travelerSubtotal, travelerDiscount))
                .Append("</strong></div><div class=\"total\"><strong>Traveler total</strong><strong>R")
                .Append(FormatPrice(travelerTotal)).Append("</strong></div></div></section>");
        }

        html.Append("<div class=\"totals\"><div class=\"total\"><span>Subtotal</span><strong>R")
            .Append(FormatPrice(quote.Subtotal)).Append("</strong></div><div class=\"total\"><span>Savings</span><strong>R")
            .Append(FormatPrice(quote.DiscountAmount)).Append("</strong></div><div class=\"total\"><span>Percentage saved/lost</span><strong>")
            .Append(FormatSavingsPercentage(quote.Subtotal, quote.DiscountAmount))
            .Append("</strong></div><p>")
            .Append(Encode(quote.DiscountDescription))
            .Append("</p><div class=\"total\"><strong>Total due for all travelers</strong><strong>R")
            .Append(FormatPrice(quote.Total)).Append("</strong></div></div></section>");
    }

    private static void AppendSelect<T>(
        StringBuilder html,
        string name,
        string id,
        string label,
        string selectedValue,
        Func<T, string>? display = null,
        bool required = false,
        string? dataField = null)
        where T : struct, Enum
    {
        html.Append("<label for=\"").Append(id).Append("\">").Append(Encode(label))
            .Append("</label><select id=\"").Append(id).Append("\" name=\"").Append(name)
            .Append('"').Append(required ? " data-required" : string.Empty);
        if (dataField is not null)
        {
            html.Append(" data-field=\"").Append(dataField).Append('"');
        }

        html.Append('>')
            .Append("<option value=\"\">Choose an option</option>");
        foreach (var option in Enum.GetValues<T>())
        {
            html.Append("<option value=\"").Append(option).Append('"')
                .Append(string.Equals(option.ToString(), selectedValue, StringComparison.OrdinalIgnoreCase)
                    ? " selected"
                    : string.Empty)
                .Append('>').Append(Encode(display?.Invoke(option) ?? option.ToString())).Append("</option>");
        }

        html.Append("</select>");
    }

    private static void AppendAirportSelect(
        StringBuilder html,
        string index,
        string field,
        string label,
        string selectedCode,
        bool required = true,
        string? dataField = null)
    {
        var name = $"{field}_{index}";
        var id = $"{field}-{index}";
        html.Append("<label for=\"").Append(id).Append("\">").Append(Encode(label))
            .Append("</label><select id=\"").Append(id).Append("\" name=\"").Append(name)
            .Append('"').Append(required ? " data-required" : string.Empty);
        if (dataField is not null)
        {
            html.Append(" data-field=\"").Append(dataField).Append('"');
        }

        html.Append("><option value=\"\">Choose an airport</option>");
        foreach (var airport in AirportCodeCatalog.SupportedAirports)
        {
            html.Append("<option value=\"").Append(airport.Code).Append('"')
                .Append(" data-country=\"").Append(airport.CountryCode)
                .Append("\" data-country-name=\"").Append(Encode(airport.CountryName))
                .Append("\" data-regional=\"")
                .Append(TravelClassificationService.IsRegionalCountry(airport.CountryCode) ? "true" : "false")
                .Append('"')
                .Append(string.Equals(airport.Code, selectedCode, StringComparison.OrdinalIgnoreCase)
                    ? " selected"
                    : string.Empty)
                .Append('>').Append(Encode($"{airport.Code} — {airport.Name}, {airport.CountryName}")).Append("</option>");
        }

        html.Append("</select>");
    }

    private static void AppendDomesticRouteSelect(
        StringBuilder html,
        string index,
        string originAirportCode,
        string destinationAirportCode)
    {
        html.Append("<label for=\"domestic-route-").Append(index)
            .Append("\">South African domestic route</label><select id=\"domestic-route-")
            .Append(index).Append("\" data-field=\"domesticRoute\"><option value=\"\">Choose a domestic route</option>");
        foreach (var route in DomesticFlightRouteCatalog.SupportedRoutes)
        {
            var selected = string.Equals(route.OriginAirportCode, originAirportCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(route.DestinationAirportCode, destinationAirportCode, StringComparison.OrdinalIgnoreCase)
                ? " selected"
                : string.Empty;
            html.Append("<option value=\"")
                .Append(route.OriginAirportCode).Append('-').Append(route.DestinationAirportCode)
                .Append('"').Append(selected).Append('>')
                .Append(Encode(route.DisplayName)).Append("</option>");
        }

        html.Append("</select>");
    }

    private static void AppendDateTime(
        StringBuilder html,
        string index,
        string field,
        string label,
        IFormCollection? form)
    {
        var name = $"{field}_{index}";
        var value = Value(form, name);
        html.Append("<label for=\"").Append(name).Append("\">").Append(Encode(label))
            .Append("</label><input id=\"").Append(name).Append("\" name=\"").Append(name)
            .Append("\" type=\"datetime-local\" data-required value=\"")
            .Append(Encode(value)).Append("\">");
    }

    private static bool IsSectorSelected(
        IFormCollection? form,
        string index,
        TravelSector sector,
        IReadOnlyCollection<TravelSector> preferences) =>
        form is null
            ? preferences.Contains(sector)
            : form[$"sectors_{index}"].Any(value => string.Equals(value, sector.ToString(), StringComparison.OrdinalIgnoreCase));

    private static DateTime ParseDateTime(IFormCollection form, string name, string description, int travelerIndex)
    {
        var value = Value(form, name);
        if (!DateTime.TryParseExact(
            value,
            "yyyy-MM-dd'T'HH:mm",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var dateTime))
        {
            throw new FormatException($"Enter a valid {description} date and time for traveler {travelerIndex + 1}.");
        }

        return dateTime;
    }

    private static T? ParseOptionalEnum<T>(string value)
        where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Enum.TryParse<T>(value, true, out var parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new FormatException($"'{value}' is not a supported {typeof(T).Name}.");
    }

    private static bool IsChecked(IFormCollection? form, string name) =>
        form is not null && string.Equals(Value(form, name), "true", StringComparison.OrdinalIgnoreCase);

    private static string Value(IFormCollection? form, string key, string fallback = "", string? repeatedIndex = null)
    {
        if (form is null)
        {
            return fallback;
        }

        if (repeatedIndex is not null)
        {
            if (int.TryParse(repeatedIndex, NumberStyles.None, CultureInfo.InvariantCulture, out var index)
                && index < form[key].Count)
            {
                return form[key][index] ?? fallback;
            }

            return fallback;
        }

        return form[key].ToString();
    }

    private static string ValueAt(string?[] values, int index) =>
        index < values.Length ? values[index] ?? string.Empty : string.Empty;

    private static string FormatRoute(TravelItinerary itinerary)
    {
        static string AirportName(string code)
        {
            var airport = AirportCodeCatalog.GetAirport(code);
            return $"{airport.Name} ({airport.Code})";
        }

        var route = $"{AirportName(itinerary.OriginAirportCode)} to ";
        if (!string.IsNullOrWhiteSpace(itinerary.ConnectionAirportCode))
        {
            route += $"{AirportName(itinerary.ConnectionAirportCode)} to ";
        }

        return route + AirportName(itinerary.DestinationAirportCode);
    }

    private static string FormatReturnRoute(TravelItinerary itinerary)
    {
        var route = $"{AirportCodeCatalog.GetAirport(itinerary.DestinationAirportCode).Name} to ";
        if (!string.IsNullOrWhiteSpace(itinerary.ConnectionAirportCode))
        {
            route += $"{AirportCodeCatalog.GetAirport(itinerary.ConnectionAirportCode).Name} to ";
        }

        return route + AirportCodeCatalog.GetAirport(itinerary.OriginAirportCode).Name;
    }

    private static string Encode(string value) => HtmlEncoder.Default.Encode(value);

    private static string FormatPrice(decimal price) =>
        price.ToString("0.00", CultureInfo.InvariantCulture);

    private static string FormatSavingsPercentage(decimal subtotal, decimal discount)
    {
        var percentage = subtotal == 0m ? 0m : Math.Abs(discount) / subtotal * 100m;
        var result = discount < 0m ? "lost" : "saved";
        return $"{percentage.ToString("0.00", CultureInfo.InvariantCulture)}% {result}";
    }

    private static string FormatDateTime(DateTime dateTime) =>
        dateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
