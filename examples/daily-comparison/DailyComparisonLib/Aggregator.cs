using DailyComparisonLib.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyComparisonLib;

public class Aggregator
{
    private readonly RateContext _context;
    public Aggregator(RateContext context)
    {
        _context = context;
    }

    public async Task AggregateAsync(DateTime date, CancellationToken ct = default)
    {
        var schedules = await _context.MarketSchedules
            .Where(m => m.Date == date.Date)
            .ToListAsync(ct);

        foreach (var schedule in schedules)
        {
            var rates = await _context.Rates
                .Where(r => r.Broker == schedule.Broker && r.Symbol == schedule.Symbol)
                .Where(r => r.RateTimestamp >= schedule.OpenTime && r.RateTimestamp <= schedule.CloseTime)
                .ToListAsync(ct);

            if (rates.Count == 0) continue;

            var high = rates.Max(r => r.Ask);
            var low = rates.Min(r => r.Bid);
            var close = rates.OrderByDescending(r => r.RateTimestamp).First().Ask;

            var prev = await _context.DailyComparisons
                .Where(d => d.Broker == schedule.Broker && d.Symbol == schedule.Symbol && d.Date == date.AddDays(-1).Date)
                .FirstOrDefaultAsync(ct);

            var prevClose = prev?.Close ?? close;
            var diff = close - prevClose;

            var existing = await _context.DailyComparisons.FindAsync(new object[] { schedule.Broker, schedule.Symbol, date.Date }, ct);
            if (existing == null)
            {
                _context.DailyComparisons.Add(new DailyComparison
                {
                    Broker = schedule.Broker,
                    Symbol = schedule.Symbol,
                    Date = date.Date,
                    High = high,
                    Low = low,
                    Close = close,
                    PrevClose = prevClose,
                    Diff = diff
                });
            }
            else
            {
                existing.High = high;
                existing.Low = low;
                existing.Close = close;
                existing.PrevClose = prevClose;
                existing.Diff = diff;
            }
        }

        await _context.SaveChangesAsync(ct);
    }
}
