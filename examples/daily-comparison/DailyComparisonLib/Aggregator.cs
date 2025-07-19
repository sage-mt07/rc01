using DailyComparisonLib.Models;
using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Core.Extensions;
using System.Linq;

namespace DailyComparisonLib;

public class Aggregator
{
    private readonly KafkaKsqlContext _context;
    public Aggregator(KafkaKsqlContext context)
    {
        _context = context;
    }

    public async Task<(List<DailyComparison> DailyBars, List<RateCandle> MinuteBars)> AggregateAsync(DateTime date, CancellationToken ct = default)
    {
        var dailyBars = (await _context.Set<DailyComparison>().ToListAsync(ct))
            .Where(d => d.Date == date.Date)
            .ToList();

        var minuteBars = (await _context.Set<RateCandle>()
            .Window(1)
            .ToListAsync(ct))
            .Where(c => c.WindowStart.Date == date.Date)
            .ToList();

        return (dailyBars, minuteBars);
    }
}
