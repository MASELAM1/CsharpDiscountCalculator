using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace CsharpDiscountCalculator.PhaseB;

public static class BrowserUI
{
    public static async Task RunAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        await using var app = builder.Build();
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(Path.Combine(AppContext.BaseDirectory, "wwwroot"))
        });
        app.MapGet("/", async (HttpResponse response) =>
        {
            response.ContentType = "text/html; charset=utf-8";
            await response.WriteAsync(TravelerBookingFormPage.Render());
        });
        app.MapPost("/", async (HttpRequest request, HttpResponse response) =>
        {
            var form = await request.ReadFormAsync();
            var result = TravelerBookingFormPage.Calculate(form);
            response.ContentType = "text/html; charset=utf-8";
            await response.WriteAsync(TravelerBookingFormPage.Render(
                form,
                result.Travelers,
                result.Quote,
                result.Error));
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
}
