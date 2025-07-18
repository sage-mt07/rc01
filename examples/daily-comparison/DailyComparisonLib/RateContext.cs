using DailyComparisonLib.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyComparisonLib;

public class RateContext : DbContext
{
    public DbSet<Rate> Rates => Set<Rate>();
    public DbSet<MarketSchedule> MarketSchedules => Set<MarketSchedule>();
    public DbSet<DailyComparison> DailyComparisons => Set<DailyComparison>();

    public RateContext(DbContextOptions<RateContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Rate>().HasKey(r => new { r.Broker, r.Symbol, r.RateId });
        modelBuilder.Entity<MarketSchedule>().HasKey(m => new { m.Broker, m.Symbol, m.Date });
        modelBuilder.Entity<DailyComparison>().HasKey(d => new { d.Broker, d.Symbol, d.Date });
    }
}
