using System;
using Confluent.Kafka;
using Kafka.Ksql.Linq.Messaging.Producers.Core;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Messaging;

public class MessagingPropertyTests
{
    [Fact]
    public void KafkaDeliveryResult_Properties_RoundTrip()
    {
        var now = DateTime.UtcNow;
        var dr = new KafkaDeliveryResult
        {
            Topic = "t",
            Partition = 1,
            Offset = 2,
            Timestamp = now,
            Status = PersistenceStatus.Persisted,
            Error = null,
            Latency = TimeSpan.FromMilliseconds(5)
        };
        Assert.Equal("t", dr.Topic);
        Assert.Equal(1, dr.Partition);
        Assert.Equal(2, dr.Offset);
        Assert.Equal(now, dr.Timestamp);
        Assert.Equal(PersistenceStatus.Persisted, dr.Status);
        Assert.Null(dr.Error);
        Assert.Equal(TimeSpan.FromMilliseconds(5), dr.Latency);
    }

}
