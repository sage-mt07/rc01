using Kafka.Ksql.Linq.Core.Attributes;
using System.Collections.Generic;

namespace Kafka.Ksql.Linq.Messaging.Configuration;

/// <summary>
/// Topic-specific configuration (for both Producer and Consumer)
/// </summary>
public class TopicSection
{
    /// <summary>
    /// Topic name (optional - can also be obtained from the dictionary key)
    /// </summary>
    public string? TopicName { get; init; }

    /// <summary>
    /// Producer configuration
    /// </summary>
    public ProducerSection Producer { get; init; } = new();

    /// <summary>
    /// Consumer configuration
    /// </summary>
    public ConsumerSection Consumer { get; init; } = new();

    /// <summary>
    /// Settings for topic creation (partition count, replication factor, etc.)
    /// </summary>
    public TopicCreationSection? Creation { get; init; }
}

/// <summary>
/// Topic creation settings
/// </summary>
public class TopicCreationSection
{
    /// <summary>
    /// Number of partitions
    /// </summary>
    [DefaultValue(1)]
    public int NumPartitions { get; init; } = 1;

    /// <summary>
    /// Replication factor
    /// </summary>
    [DefaultValue(1)]
    public short ReplicationFactor { get; init; } = 1;

    /// <summary>
    /// Topic configuration (cleanup.policy, retention.ms, etc.)
    /// </summary>
    public Dictionary<string, string> Configs { get; init; } = new();

    /// <summary>
    /// Enable automatic creation
    /// </summary>
    public bool EnableAutoCreation { get; init; } = false;
}