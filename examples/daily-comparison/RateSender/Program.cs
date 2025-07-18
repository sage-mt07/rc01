using DailyComparisonLib;
using DailyComparisonLib.Models;
using Microsoft.EntityFrameworkCore;

var options = new DbContextOptionsBuilder<RateContext>()
    .UseSqlServer(Environment.GetEnvironmentVariable("CONNECTION_STRING") ?? "Server=sqlserver;Database=Rates;User Id=sa;Password=Your_password123;TrustServerCertificate=true")
    .Options;

await using var context = new RateContext(options);
context.Database.EnsureCreated();

var broker = "demo";
var symbol = "EURUSD";
var id = DateTime.UtcNow.Ticks;
var timestamp = DateTime.UtcNow;

var rate = RateGenerator.Create(broker, symbol, id, timestamp);
context.Rates.Add(rate);
await context.SaveChangesAsync();

Console.WriteLine($"Inserted rate {id} at {timestamp:O}");

var scheduleUpdater = new ScheduleUpdater(context);
await scheduleUpdater.UpdateAsync(new[]{ new MarketSchedule{
    Broker = broker,
    Symbol = symbol,
    Date = DateTime.UtcNow.Date,
    OpenTime = DateTime.UtcNow.Date,
    CloseTime = DateTime.UtcNow.Date.AddHours(24)
}}, CancellationToken.None);

var aggregator = new Aggregator(context);
await aggregator.AggregateAsync(DateTime.UtcNow.Date);


