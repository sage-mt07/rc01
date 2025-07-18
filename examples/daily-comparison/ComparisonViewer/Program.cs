using DailyComparisonLib;
using DailyComparisonLib.Models;
using Microsoft.Extensions.Logging;

var schemaUrl = Environment.GetEnvironmentVariable("SCHEMA_URL") ?? "http://schema-registry:8081";

await using var context = new MyKsqlContext(
    schemaUrl,
    LoggerFactory.Create(b => b.AddConsole()));

var comparisons = await context.Set<DailyComparison>().ToListAsync();

foreach (var c in comparisons)
{
    Console.WriteLine($"{c.Date:d} {c.Broker} {c.Symbol} High:{c.High} Low:{c.Low} Close:{c.Close} Diff:{c.Diff}");
}
