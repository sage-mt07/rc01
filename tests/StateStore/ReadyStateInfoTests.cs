using Kafka.Ksql.Linq.StateStore.Monitoring;
using System;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.StateStore;

public class ReadyStateInfoTests
{
    [Fact]
    public void ToString_ReadyStateIncludesTimeToReady()
    {
        var info = new ReadyStateInfo
        {
            TopicName = "topic",
            IsReady = true,
            TotalLag = 10,
            PartitionCount = 2,
            TimeToReady = TimeSpan.FromSeconds(5)
        };

        var text = info.ToString();

        Assert.Contains("topic", text);
        Assert.Contains("READY", text);
        Assert.Contains("10", text);
        Assert.Contains("Partitions: 2", text);
        Assert.Contains("Ready in: 5.0s", text);
    }

    [Fact]
    public void ToString_NotReadyIncludesBindingTime()
    {
        var info = new ReadyStateInfo
        {
            TopicName = "t2",
            IsReady = false,
            TotalLag = 3,
            PartitionCount = 1,
            TimeSinceBinding = TimeSpan.FromSeconds(7)
        };

        var text = info.ToString();

        Assert.Contains("t2", text);
        Assert.Contains("NOT_READY", text);
        Assert.Contains("3", text);
        Assert.Contains("Binding for: 7.0s", text);
    }
}
