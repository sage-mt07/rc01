using Kafka.Ksql.Linq.Configuration;
using Microsoft.Extensions.Configuration;

namespace Kafka.Ksql.Linq.Core.Context;

public class KafkaContextOptions
{
    /// <summary>
    /// Kafka bootstrap servers
    /// </summary>
    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>
    /// Schema Registry base URL
    /// </summary>
    public string SchemaRegistryUrl { get; set; } = "http://localhost:8081";

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

