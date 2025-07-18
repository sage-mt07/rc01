using DailyComparisonLib;
using DailyComparisonLib.Models;
using Microsoft.Extensions.Logging;

var schemaUrl = Environment.GetEnvironmentVariable("SCHEMA_URL") ?? "http://schema-registry:8081";

await using var context = new MyKsqlContext(
    schemaUrl,
    LoggerFactory.Create(b => b.AddConsole()));

var broker = "demo";
var symbol = "EURUSD";

var scheduleUpdater = new ScheduleUpdater(context);
await scheduleUpdater.UpdateAsync(new[]{ new MarketSchedule{
    Broker = broker,
    Symbol = symbol,
    Date = DateTime.UtcNow.Date,
    OpenTime = DateTime.UtcNow.Date,
    CloseTime = DateTime.UtcNow.Date.AddHours(24)
}}, CancellationToken.None);

for (int i = 0; i < 100; i++)
{
    var id = DateTime.UtcNow.Ticks;
    var timestamp = DateTime.UtcNow;
    var rate = RateGenerator.Create(broker, symbol, id, timestamp);
    await context.Set<Rate>().AddAsync(rate);
    Console.WriteLine($"Sent rate {id} at {timestamp:O}");
    await Task.Delay(1000);
}

var aggregator = new Aggregator(context);
await aggregator.AggregateAsync(DateTime.UtcNow.Date);
