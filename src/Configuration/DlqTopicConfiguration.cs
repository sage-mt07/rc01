using Kafka.Ksql.Linq.Core.Attributes;
using System;
using System.Collections.Generic;

namespace Kafka.Ksql.Linq.Configuration;

public class DlqTopicConfiguration
{
    /// <summary>
    /// DLQ data retention in milliseconds.
    /// Default: 5000 (5 seconds) - functions as temporary storage.
    /// </summary>
    [DefaultValue(5000)]
    public long RetentionMs { get; set; } = 5000;

    /// <summary>
    /// Number of partitions for the DLQ topic.
    /// Default: 1 (for observability rather than performance)
    /// </summary>
    [DefaultValue(1)]
    public int NumPartitions { get; set; } = 1;

    /// <summary>
    /// Replication factor for the DLQ topic.
    /// Default: 1 (suitable for single-broker environments)
    /// </summary>
    [DefaultValue(1)]
    public short ReplicationFactor { get; set; } = 1;

    /// <summary>
    /// Whether to enable automatic DLQ topic creation.
    /// Default: true (avoids fail-fast on missing topic)
    /// </summary>
    [DefaultValue(true)]
    public bool EnableAutoCreation { get; set; } = true;

    /// <summary>
    /// Additional DLQ topic settings
    /// e.g., cleanup.policy, segment.ms, max.message.bytes, etc.
    /// </summary>
    public Dictionary<string, string> AdditionalConfigs { get; set; } = new()
    {
        ["cleanup.policy"] = "delete",
          ["segment.ms"] = "3600000", // 1 hour
          ["max.message.bytes"] = "1048576", // 1MB
        ["confluent.topic.description"] = "DLQ topic for error message handling - auto created by KafkaContext"
    };

    /// <summary>
    /// Validate DLQ configuration
    /// </summary>
    public void Validate()
    {
        if (RetentionMs <= 0)
            throw new ArgumentException("DLQ RetentionMs must be positive", nameof(RetentionMs));

        if (NumPartitions <= 0)
            throw new ArgumentException("DLQ NumPartitions must be positive", nameof(NumPartitions));

        if (ReplicationFactor <= 0)
            throw new ArgumentException("DLQ ReplicationFactor must be positive", nameof(ReplicationFactor));
    }

    /// <summary>
    /// Get configuration summary (for debugging)
    /// </summary>
    public string GetSummary()
    {
        return $"DLQ Config: Retention={RetentionMs}ms, Partitions={NumPartitions}, " +
               $"Replicas={ReplicationFactor}, AutoCreate={EnableAutoCreation}";
    }
}
public static class DlqConfigurationExtensions
{
    /// <summary>
    /// Customize the DLQ configuration
    /// </summary>
    public static KsqlDslOptions ConfigureDlq(
        this KsqlDslOptions options,
        Action<DlqTopicConfiguration> configureDlq)
    {
        configureDlq?.Invoke(options.DlqConfiguration);
        options.DlqConfiguration.Validate();
        return options;
    }

    /// <summary>
    /// Set DLQ retention time (convenience method)
    /// </summary>
    public static KsqlDslOptions WithDlqRetention(
        this KsqlDslOptions options,
        TimeSpan retention)
    {
        options.DlqConfiguration.RetentionMs = (long)retention.TotalMilliseconds;
        return options;
    }

    /// <summary>
    /// Disable the DLQ (e.g., for testing environments)
    /// </summary>
    public static KsqlDslOptions DisableDlqAutoCreation(this KsqlDslOptions options)
    {
        options.DlqConfiguration.EnableAutoCreation = false;
        return options;
    }
}
