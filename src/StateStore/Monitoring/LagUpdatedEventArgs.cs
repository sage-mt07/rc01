using System;
using System.Collections.Generic;

namespace Kafka.Ksql.Linq.StateStore.Monitoring;

internal class LagUpdatedEventArgs : EventArgs
{
    public string TopicName { get; set; } = string.Empty;
    public long TotalLag { get; set; }
    public bool IsReady { get; set; }
    public Dictionary<int, long> PartitionLags { get; set; } = new();
}
