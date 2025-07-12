using Kafka.Ksql.Linq.Configuration;

namespace Kafka.Ksql.Linq.Core.Context;

public class KafkaContextOptions
{

    public ValidationMode ValidationMode { get; set; } = ValidationMode.Strict;

}