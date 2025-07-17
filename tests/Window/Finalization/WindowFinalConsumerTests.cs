using Kafka.Ksql.Linq.Cache.Core;
using Kafka.Ksql.Linq.Window.Finalization;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Window.Finalization;

public class WindowFinalConsumerTests
{
    [Fact]
    public void GetFinalizedWindow_ReturnsStoredMessage()
    {
        var cache = new RocksDbTableCache<WindowFinalMessage>();
        var storeField = typeof(RocksDbTableCache<WindowFinalMessage>).GetField("_store", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (ConcurrentDictionary<string, WindowFinalMessage>)storeField.GetValue(cache)!;
        var msg = new WindowFinalMessage { WindowKey = "w", WindowMinutes = 5, WindowStart = DateTime.UtcNow, WindowEnd = DateTime.UtcNow.AddMinutes(5), PodId = "p" };
        dict["w"] = msg;
        var consumer = new WindowFinalConsumer(cache, NullLoggerFactory.Instance);

        var result = consumer.GetFinalizedWindow("w");

        Assert.Equal(msg.WindowKey, result?.WindowKey);
    }

    [Fact]
    public void GetFinalizedWindowsBySize_FiltersByWindowMinutes()
    {
        var cache = new RocksDbTableCache<WindowFinalMessage>();
        var storeField = typeof(RocksDbTableCache<WindowFinalMessage>).GetField("_store", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (ConcurrentDictionary<string, WindowFinalMessage>)storeField.GetValue(cache)!;
        var m1 = new WindowFinalMessage { WindowKey = "w1", WindowMinutes = 5, WindowStart = DateTime.UtcNow, WindowEnd = DateTime.UtcNow.AddMinutes(5), PodId = "p" };
        var m2 = new WindowFinalMessage { WindowKey = "w2", WindowMinutes = 10, WindowStart = DateTime.UtcNow, WindowEnd = DateTime.UtcNow.AddMinutes(10), PodId = "p" };
        dict["w1"] = m1;
        dict["w2"] = m2;
        var consumer = new WindowFinalConsumer(cache, NullLoggerFactory.Instance);

        var list = consumer.GetFinalizedWindowsBySize(5);

        Assert.Single(list);
        Assert.Equal("w1", list[0].WindowKey);
    }
}
