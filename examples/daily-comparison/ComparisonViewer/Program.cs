using DailyComparisonLib;
using Microsoft.EntityFrameworkCore;

var options = new DbContextOptionsBuilder<RateContext>()
    .UseSqlServer(Environment.GetEnvironmentVariable("CONNECTION_STRING") ?? "Server=sqlserver;Database=Rates;User Id=sa;Password=Your_password123;TrustServerCertificate=true")
    .Options;

await using var context = new RateContext(options);
var comparisons = context.DailyComparisons.ToList();

foreach (var c in comparisons)
{
    Console.WriteLine($"{c.Date:d} {c.Broker} {c.Symbol} High:{c.High} Low:{c.Low} Close:{c.Close} Diff:{c.Diff}");
}
