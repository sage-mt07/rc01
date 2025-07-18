using DailyComparisonLib.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyComparisonLib;

public class ScheduleUpdater
{
    private readonly RateContext _context;
    public ScheduleUpdater(RateContext context)
    {
        _context = context;
    }

    public async Task UpdateAsync(IEnumerable<MarketSchedule> schedules, CancellationToken ct = default)
    {
        foreach (var s in schedules)
        {
            var existing = await _context.MarketSchedules.FindAsync(new object[] { s.Broker, s.Symbol, s.Date }, ct);
            if (existing == null)
            {
                _context.MarketSchedules.Add(s);
            }
            else
            {
                existing.OpenTime = s.OpenTime;
                existing.CloseTime = s.CloseTime;
            }
        }
        await _context.SaveChangesAsync(ct);
    }
}
