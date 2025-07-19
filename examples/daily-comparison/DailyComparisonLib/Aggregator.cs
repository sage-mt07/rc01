using DailyComparisonLib.Models;
using Kafka.Ksql.Linq;
using System.Linq;

namespace DailyComparisonLib;

public class Aggregator
{
    private readonly KafkaKsqlContext _context;
    public Aggregator(KafkaKsqlContext context)
    {
        _context = context;
    }

    private static DateTime FloorToMinutes(DateTime timestamp, int minutes)
    {
        var ticks = TimeSpan.FromMinutes(minutes).Ticks;
        return new DateTime(timestamp.Ticks - (timestamp.Ticks % ticks), timestamp.Kind);
    }

    public async Task AggregateAsync(DateTime date, CancellationToken ct = default)
    {
        var schedules = (await _context.Set<MarketSchedule>().ToListAsync(ct))
            .Where(m => m.Date == date.Date)
            .ToList();

        foreach (var schedule in schedules)
        {
            var rates = await _context.Set<Rate>()
                .Where(r => r.Broker == schedule.Broker && r.Symbol == schedule.Symbol)
                .Window().BaseOn<MarketSchedule>(r => r.Symbol, nameof(MarketSchedule.OpenTime), nameof(MarketSchedule.CloseTime))
                .ToListAsync(ct);

            if (rates.Count == 0) continue;

            var high = rates.Max(r => r.Ask);
            var low = rates.Min(r => r.Bid);
            var close = rates.OrderByDescending(r => r.RateTimestamp).First().Ask;

            var prev = (await _context.Set<DailyComparison>().ToListAsync(ct))
                .FirstOrDefault(d => d.Broker == schedule.Broker && d.Symbol == schedule.Symbol && d.Date == date.AddDays(-1).Date);

            var prevClose = prev?.Close ?? close;
            var diff = close - prevClose;

            await _context.Set<DailyComparison>().AddAsync(new DailyComparison
            {
                Broker = schedule.Broker,
                Symbol = schedule.Symbol,
                Date = date.Date,
                High = high,
                Low = low,
                Close = close,
                PrevClose = prevClose,
                Diff = diff
            }, ct);

            foreach (var minutes in new[] { 1, 5, 60 })
            {
                // TODO: This manual grouping should be replaced with the
                // built-in Window() aggregation once documentation is clarified.
                var grouped = rates.GroupBy(r => FloorToMinutes(r.RateTimestamp, minutes));
                foreach (var g in grouped)
                {
                    var open = g.OrderBy(r => r.RateTimestamp).First().Ask;
                    var highM = g.Max(r => r.Ask);
                    var lowM = g.Min(r => r.Bid);
                    var closeM = g.OrderByDescending(r => r.RateTimestamp).First().Ask;

                    await _context.Set<RateCandle>().AddAsync(new RateCandle
                    {
                        Broker = schedule.Broker,
                        Symbol = schedule.Symbol,
                        WindowStart = g.Key,
                        WindowEnd = g.Key.AddMinutes(minutes),
                        WindowMinutes = minutes,
                        Open = open,
                        High = highM,
                        Low = lowM,
                        Close = closeM
                    }, ct);
                }
            }
        }

    }
}
