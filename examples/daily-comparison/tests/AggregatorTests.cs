using DailyComparisonLib;
using DailyComparisonLib.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyComparisonLib.Tests;

public class AggregatorTests
{
    [Fact]
    public async Task Aggregate_ComputesDailyComparison()
    {
        var options = new DbContextOptionsBuilder<RateContext>()
            .UseInMemoryDatabase("test")
            .Options;

        await using var context = new RateContext(options);
        context.Rates.Add(new Rate { Broker = "b", Symbol = "s", RateId = 1, RateTimestamp = new DateTime(2024,1,1,1,0,0), Bid = 1m, Ask = 1.1m });
        context.Rates.Add(new Rate { Broker = "b", Symbol = "s", RateId = 2, RateTimestamp = new DateTime(2024,1,1,2,0,0), Bid = 2m, Ask = 2.1m });
        context.MarketSchedules.Add(new MarketSchedule { Broker="b", Symbol="s", Date = new DateTime(2024,1,1), OpenTime = new DateTime(2024,1,1,0,0,0), CloseTime = new DateTime(2024,1,1,23,59,59)});
        await context.SaveChangesAsync();

        var aggregator = new Aggregator(context);
        await aggregator.AggregateAsync(new DateTime(2024,1,1));

        var result = Assert.Single(context.DailyComparisons);
        Assert.Equal(2.1m, result.High);
        Assert.Equal(1m, result.Low);
        Assert.Equal(2.1m, result.Close);
        Assert.Equal(0m, result.Diff);
    }
}
