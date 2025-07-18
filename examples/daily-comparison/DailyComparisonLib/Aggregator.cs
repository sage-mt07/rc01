using DailyComparisonLib.Models;
using Kafka.Ksql.Linq;

namespace DailyComparisonLib;

public class Aggregator
{
    private readonly KafkaKsqlContext _context;
    public Aggregator(KafkaKsqlContext context)
    {
        _context = context;
    }

    public async Task AggregateAsync(DateTime date, CancellationToken ct = default)
    {
        var schedules = (await _context.Set<MarketSchedule>().ToListAsync(ct))
            .Where(m => m.Date == date.Date)
            .ToList();

        foreach (var schedule in schedules)
        {
            var rates = (await _context.Set<Rate>().ToListAsync(ct))
                .Where(r => r.Broker == schedule.Broker && r.Symbol == schedule.Symbol)
                .Where(r => r.RateTimestamp >= schedule.OpenTime && r.RateTimestamp <= schedule.CloseTime)
                .ToList();

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
        }

    }
}
