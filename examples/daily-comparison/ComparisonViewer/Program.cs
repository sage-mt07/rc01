using DailyComparisonLib;
using DailyComparisonLib.Models;

await using var context = MyKsqlContext.FromAppSettings("appsettings.json");

var comparisons = await context.Set<DailyComparison>().ToListAsync();

foreach (var c in comparisons)
{
    Console.WriteLine($"{c.Date:d} {c.Broker} {c.Symbol} High:{c.High} Low:{c.Low} Close:{c.Close} Diff:{c.Diff}");
}
