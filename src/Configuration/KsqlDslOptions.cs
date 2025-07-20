using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Attributes;
using Kafka.Ksql.Linq.Messaging.Configuration;
using Kafka.Ksql.Linq.Core.Configuration;
using System.Collections.Generic;

namespace Kafka.Ksql.Linq.Configuration;
public class KsqlDslOptions
{
    /// <summary>
    /// Validation mode (root level setting)
    /// </summary>
    public ValidationMode ValidationMode { get; init; } = ValidationMode.Strict;

    /// <summary>
    /// Common settings (BootstrapServers, ClientId, etc.)
    /// </summary>
    public CommonSection Common { get; init; } = new();

    /// <summary>
    /// Per-topic settings (manage producer/consumer settings per topic)
    /// </summary>
    public Dictionary<string, TopicSection> Topics { get; init; } = new();

    /// <summary>
    /// Schema Registry settings
    /// </summary>
    public SchemaRegistrySection SchemaRegistry { get; init; } = new();

    public List<EntityConfiguration> Entities { get; init; } = new();

    [DefaultValue("dead.letter.queue")]
    public string DlqTopicName { get; set; } = "dead.letter.queue";

    public DlqTopicConfiguration DlqConfiguration { get; init; } = new();

    /// <summary>
    /// Policy when deserialization fails
    /// </summary>
    public DeserializationErrorPolicy DeserializationErrorPolicy { get; set; } = DeserializationErrorPolicy.Skip;

    /// <summary>
    /// Whether reading from the Final topic is enabled by default
    /// </summary>
    public bool ReadFromFinalTopicByDefault { get; set; } = false;

    /// <summary>
    /// Optional bar limits per symbol and bar type
    /// </summary>
    public BarLimitOptions BarLimits { get; init; } = new();

    /// <summary>
    /// Global decimal precision applied when mapping decimal properties.
    /// </summary>
    [DefaultValue(38)]
    public int DecimalPrecision { get; init; } = 38;

    /// <summary>
    /// Global decimal scale applied when mapping decimal properties.
    /// </summary>
    [DefaultValue(9)]
    public int DecimalScale { get; init; } = 9;
}
