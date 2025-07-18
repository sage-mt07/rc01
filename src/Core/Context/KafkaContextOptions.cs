using Kafka.Ksql.Linq.Configuration;
using Microsoft.Extensions.Configuration;

namespace Kafka.Ksql.Linq.Core.Context;

public class KafkaContextOptions
{
    public ValidationMode ValidationMode { get; set; } = ValidationMode.Strict;

    public IConfiguration? Configuration { get; set; }

    public static KafkaContextOptions FromConfiguration(IConfiguration configuration)
    {
        return new KafkaContextOptions { Configuration = configuration };
    }

    public static KafkaContextOptions FromAppSettings(string path)
    {
        var configuration = new ConfigurationBuilder().AddJsonFile(path).Build();
        return FromConfiguration(configuration);
    }
}

