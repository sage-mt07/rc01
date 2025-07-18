using Kafka.Ksql.Linq.Core.Attributes;
using System.Collections.Generic;

namespace Kafka.Ksql.Linq.Messaging.Configuration;
public class ProducerSection
{
    /// <summary>
    /// Acknowledgement level (All, Leader, None)
    /// </summary>
    [DefaultValue("All")]
    public string Acks { get; init; } = "All";

    /// <summary>
    /// Compression type
    /// </summary>
    [DefaultValue("Snappy")]
    public string CompressionType { get; init; } = "Snappy";

    /// <summary>
    /// Enable idempotence
    /// </summary>
    [DefaultValue(true)]
    public bool EnableIdempotence { get; init; } = true;

    /// <summary>
    /// Maximum in-flight requests
    /// </summary>
    public int MaxInFlightRequestsPerConnection { get; init; } = 1;

    /// <summary>
    /// Delay before batch send (ms)
    /// </summary>
    public int LingerMs { get; init; } = 5;

    /// <summary>
    /// Batch size (bytes)
    /// </summary>
    public int BatchSize { get; init; } = 16384;

    /// <summary>
    /// Delivery timeout (ms)
    /// </summary>
    public int DeliveryTimeoutMs { get; init; } = 120000;

    /// <summary>
    /// Retry backoff time (ms)
    /// </summary>
    public int RetryBackoffMs { get; init; } = 100;

    /// <summary>
    /// Maximum retry count
    /// </summary>
    public int Retries { get; init; } = int.MaxValue;

    /// <summary>
    /// Buffer memory (bytes)
    /// </summary>
    public long BufferMemory { get; init; } = 33554432; // 32MB

    /// <summary>
    /// Partitioner
    /// </summary>
    public string? Partitioner { get; init; }

    /// <summary>
    /// Additional configuration properties
    /// </summary>
    public Dictionary<string, string> AdditionalProperties { get; init; } = new();
}
