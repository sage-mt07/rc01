using System.Collections.Generic;

namespace Kafka.Ksql.Linq.Messaging.Configuration;

/// <summary>
/// Consumer configuration
/// </summary>
public class ConsumerSection
{
    /// <summary>
    /// Consumer group ID
    /// </summary>
    public string GroupId { get; init; } = string.Empty;

    /// <summary>
    /// Auto offset reset (Latest, Earliest, None)
    /// </summary>
    public string AutoOffsetReset { get; init; } = "Latest";

    /// <summary>
    /// Enable auto commit
    /// </summary>
    public bool EnableAutoCommit { get; init; } = true;

    /// <summary>
    /// Auto commit interval (ms)
    /// </summary>
    public int AutoCommitIntervalMs { get; init; } = 5000;

    /// <summary>
    /// Session timeout (ms)
    /// </summary>
    public int SessionTimeoutMs { get; init; } = 30000;

    /// <summary>
    /// Heartbeat interval (ms)
    /// </summary>
    public int HeartbeatIntervalMs { get; init; } = 3000;

    /// <summary>
    /// Max poll interval (ms)
    /// </summary>
    public int MaxPollIntervalMs { get; init; } = 300000;

    /// <summary>
    /// Max poll records
    /// </summary>
    public int MaxPollRecords { get; init; } = 500;

    /// <summary>
    /// Fetch minimum bytes
    /// </summary>
    public int FetchMinBytes { get; init; } = 1;

    /// <summary>
    /// Fetch max wait (ms)
    /// </summary>
    public int FetchMaxWaitMs { get; init; } = 500;

    /// <summary>
    /// Fetch max bytes
    /// </summary>
    public int FetchMaxBytes { get; init; } = 52428800; // 50MB

    /// <summary>
    /// Partition assignment strategy
    /// </summary>
    public string? PartitionAssignmentStrategy { get; init; }

    /// <summary>
    /// Isolation level
    /// </summary>
    public string IsolationLevel { get; init; } = "ReadUncommitted";

    /// <summary>
    /// Additional configuration properties
    /// </summary>
    public Dictionary<string, string> AdditionalProperties { get; init; } = new();
}