using DailyComparisonLib;
using DailyComparisonLib.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var loggerFactory = LoggerFactory.Create(b =>
{
    b.AddConfiguration(configuration.GetSection("Logging"));
    b.AddConsole();
});

await using var context = KsqlContextBuilder.Create()
    .UseConfiguration(configuration)
    .UseSchemaRegistry(configuration["KsqlDsl:SchemaRegistry:Url"]!)
    .EnableLogging(loggerFactory)
    .BuildContext<MyKsqlContext>();

var comparisons = await context.Set<DailyComparison>().ToListAsync();

foreach (var c in comparisons)
{
    Console.WriteLine($"{c.Date:d} {c.Broker} {c.Symbol} High:{c.High} Low:{c.Low} Close:{c.Close} Diff:{c.Diff}");
}
