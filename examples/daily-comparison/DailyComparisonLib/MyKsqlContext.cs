using Kafka.Ksql.Linq.Application;
using Microsoft.Extensions.Logging;

namespace DailyComparisonLib;

public class MyKsqlContext : KafkaKsqlContext
{
    private readonly string _schemaRegistryUrl;
    private readonly ILoggerFactory _loggerFactory;

    public MyKsqlContext(string schemaRegistryUrl, ILoggerFactory loggerFactory)
    {
        _schemaRegistryUrl = schemaRegistryUrl;
        _loggerFactory = loggerFactory;
    }
}
