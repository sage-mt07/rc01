using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Context;
using DailyComparisonLib.Models;

namespace DailyComparisonLib;

public class KafkaKsqlContext : KafkaContext
{

    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Rate>();
        modelBuilder.Entity<MarketSchedule>();
        modelBuilder.Entity<DailyComparison>();
    }
}
