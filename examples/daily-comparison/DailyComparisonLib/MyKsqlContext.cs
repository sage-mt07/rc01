using Kafka.Ksql.Linq.Application;

namespace DailyComparisonLib;

public class MyKsqlContext : KafkaKsqlContext
{
    public MyKsqlContext(KsqlContextOptions options) : base(options)
    {
    }
}
