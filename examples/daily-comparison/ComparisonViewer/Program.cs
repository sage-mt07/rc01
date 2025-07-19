using DailyComparisonLib;
using DailyComparisonLib.Models;
using Kafka.Ksql.Linq.Core.Extensions;

await using var context = MyKsqlContext.FromAppSettings("appsettings.json");

var oneMinBars = await context.Set<RateCandle>().Window(1).ToListAsync();
var fiveMinBars = await context.Set<RateCandle>().Window(5).ToListAsync();
var dailyBars = await context.Set<RateCandle>().Window(1440).ToListAsync();
var comparisons = await context.Set<DailyComparison>().ToListAsync();

Console.WriteLine("--- 1 minute bars ---");
foreach (var c in oneMinBars)
{
    Console.WriteLine($"1m {c.BarTime:t} {c.Symbol} O:{c.Open} H:{c.High} L:{c.Low} C:{c.Close}");
}

Console.WriteLine("--- 5 minute bars ---");
foreach (var c in fiveMinBars)
{
    Console.WriteLine($"5m {c.BarTime:t} {c.Symbol} O:{c.Open} H:{c.High} L:{c.Low} C:{c.Close}");
}

Console.WriteLine("--- Daily bars ---");
foreach (var c in dailyBars)
{
    Console.WriteLine($"day {c.BarTime:d} {c.Symbol} O:{c.Open} H:{c.High} L:{c.Low} C:{c.Close}");
}

Console.WriteLine("--- Daily comparison ---");
foreach (var c in comparisons)
{
    Console.WriteLine($"{c.Date:d} {c.Broker} {c.Symbol} High:{c.High} Low:{c.Low} Close:{c.Close} Diff:{c.Diff}");
}
