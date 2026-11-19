using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CsharpDiscountCalculator;

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
            var pricesInput = form["prices"].ToString();
            var collectionName = form["collection"].ToString();
            IReadOnlyList<DiscountResult>? results = null;
            string? error = null;

            try
            {
                if (collectionName is not ("Array" or "Linked list"))
                {
                    throw new ArgumentException("Choose an array or linked list.");
                }

                var prices = DiscountCalculator.ParsePrices(pricesInput);
                var calculator = new DiscountCalculator();

                if (collectionName == "Linked list")
                {
                    var linkedList = new LinkedList<decimal>(prices);
                    results = calculator.Calculate(linkedList);
                }
                else
                {
                    results = calculator.Calculate(prices);
                }
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                error = exception.Message;
            }

            response.ContentType = "text/html; charset=utf-8";
            await response.WriteAsync(RenderPage(pricesInput, collectionName, results, error));
        });

        await app.StartAsync();
        var address = app.Services
            .GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.Single();

        Console.WriteLine($"Browser interface is available at {address}");
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
        string pricesInput = "",
        string collectionName = "Array",
        IReadOnlyList<DiscountResult>? results = null,
        string? error = null)
    {
        var html = new StringBuilder("""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Discount Calculator</title>
              <style>
                body { font-family: system-ui, sans-serif; max-width: 760px; margin: 3rem auto; padding: 0 1rem; color: #222; }
                h1 { margin-bottom: .4rem; }
                .rules { color: #555; margin-bottom: 1.5rem; }
                label { display: block; font-weight: 600; margin: 1rem 0 .4rem; }
                input, select, button { font: inherit; padding: .6rem; }
                input { box-sizing: border-box; width: 100%; }
                button { margin-top: 1rem; cursor: pointer; }
                .error { color: #a00000; margin-top: 1rem; }
                table { border-collapse: collapse; margin-top: 1.5rem; width: 100%; }
                th, td { border-bottom: 1px solid #ccc; padding: .65rem; text-align: right; }
                th:first-child, td:first-child { text-align: left; }
              </style>
            </head>
            <body>
              <h1>Discount Calculator</h1>
              <p class="rules">Prices above R20 and below R50 receive 10% off. Prices at or above R50 receive 25% off. Prices at or below R20 receive no discount.</p>
              <form method="post">
                <label for="prices">Prices</label>
                <input id="prices" name="prices" value="
            """).Append(HtmlEncoder.Default.Encode(pricesInput)).Append("""
            " placeholder="12.30, 33.89, 56.10" required>
                <label for="collection">Price collection</label>
                <select id="collection" name="collection">
            """);

        AddOption(html, "Array", collectionName);
        AddOption(html, "Linked list", collectionName);
        html.Append("""
                </select>
                <button type="submit">Calculate discounts</button>
              </form>
            """);

        if (error is not null)
        {
            html.Append("<p class=\"error\">Error: ")
                .Append(HtmlEncoder.Default.Encode(error))
                .Append("</p>");
        }

        if (results is not null)
        {
            html.Append("""
              <table>
                <thead><tr><th>Original</th><th>Discount</th><th>Discounted</th></tr></thead>
                <tbody>
            """);

            foreach (var result in results)
            {
                html.Append("<tr><td>R")
                    .Append(FormatPrice(result.OriginalPrice))
                    .Append("</td><td>R")
                    .Append(FormatPrice(result.DiscountAmount))
                    .Append("</td><td>R")
                    .Append(FormatPrice(result.DiscountedPrice))
                    .Append("</td></tr>");
            }

            html.Append("</tbody></table>");
        }

        return html.Append("</body></html>").ToString();
    }

    private static void AddOption(StringBuilder html, string value, string selectedValue)
    {
        html.Append("<option value=\"")
            .Append(value)
            .Append('"')
            .Append(value == selectedValue ? " selected" : string.Empty)
            .Append('>')
            .Append(value)
            .Append("</option>");
    }

    private static string FormatPrice(decimal price) =>
        price.ToString("0.00", CultureInfo.InvariantCulture);
}
