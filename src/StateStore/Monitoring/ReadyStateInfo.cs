using Confluent.Kafka;
using System;
using System.Collections.Generic;

namespace Kafka.Ksql.Linq.StateStore.Monitoring;

internal class ReadyStateInfo
{
    public string TopicName { get; set; } = string.Empty;
    public bool IsReady { get; set; }
    public long TotalLag { get; set; }
    public List<TopicPartitionOffset> EndOffsets { get; set; } = new();
    public List<TopicPartitionOffset> CurrentOffsets { get; set; } = new();
    public TimeSpan TimeSinceBinding { get; set; }
    public TimeSpan? TimeToReady { get; set; }
    public int PartitionCount { get; set; }

    public override string ToString()
    {
        var readyStatus = IsReady ? "READY" : "NOT_READY";
        var timeInfo = TimeToReady.HasValue
            ? $"Ready in: {TimeToReady.Value.TotalSeconds:F1}s"
            : $"Binding for: {TimeSinceBinding.TotalSeconds:F1}s";

        return $"Topic: {TopicName}, Status: {readyStatus}, Lag: {TotalLag}, Partitions: {PartitionCount}, {timeInfo}";
    }
}
