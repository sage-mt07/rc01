using System;

namespace Kafka.Ksql.Linq.Configuration.Abstractions;
public class KafkaBatchOptions
{
    /// <summary>
    /// Maximum batch size
    /// </summary>
    public int MaxBatchSize { get; set; } = 100;

    /// <summary>
    /// Maximum wait time
    /// </summary>
    public TimeSpan MaxWaitTime { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to allow empty batches
    /// </summary>
    public bool EnableEmptyBatches { get; set; } = false;

    /// <summary>
    /// Enable auto commit
    /// </summary>
    public bool AutoCommit { get; set; } = true;

    /// <summary>
    /// Consumer group ID
    /// </summary>
    public string? ConsumerGroupId { get; set; }
}
