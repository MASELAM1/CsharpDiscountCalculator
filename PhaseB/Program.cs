using CsharpDiscountCalculator.PhaseB;

if (args.Length == 2
    && Path.GetExtension(args[0]).Equals(".csproj", StringComparison.OrdinalIgnoreCase)
    && args[1] == "--browser")
{
    args = [args[1]];
}

if (args.Length > 0 && (args.Length != 1 || args[0] != "--browser"))
{
    Console.Error.WriteLine("Usage: dotnet run --project PhaseB/CsharpDiscountCalculator.PhaseB.csproj [--browser]");
    Environment.ExitCode = 1;
    return;
}

await BrowserUI.RunAsync();
