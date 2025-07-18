using System;

namespace Kafka.Ksql.Linq.Configuration.Abstractions;
public class KafkaFetchOptions
{
    /// <summary>
    /// Maximum number of records
    /// </summary>
    public int MaxRecords { get; set; } = 100;

    /// <summary>
    /// Timeout duration
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Consumer group ID
    /// </summary>
    public string? GroupId { get; set; }
}
