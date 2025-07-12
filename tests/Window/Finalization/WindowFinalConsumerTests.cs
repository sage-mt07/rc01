using Kafka.Ksql.Linq.StateStore.Core;
using Kafka.Ksql.Linq.StateStore.Configuration;
using Kafka.Ksql.Linq.Window.Finalization;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Window.Finalization;

public class WindowFinalConsumerTests
{
    [Fact]
    public void GetFinalizedWindow_ReturnsStoredMessage()
    {
        var store = new RocksDbStateStore<string, WindowFinalMessage>("test", new StateStoreOptions { EnableCache = false }, NullLoggerFactory.Instance);
        var consumer = new WindowFinalConsumer(store, NullLoggerFactory.Instance);
        var msg = new WindowFinalMessage { WindowKey = "w", WindowMinutes = 5, WindowStart = DateTime.UtcNow, WindowEnd = DateTime.UtcNow.AddMinutes(5), PodId = "p" };
        store.Put("w", msg);

        var result = consumer.GetFinalizedWindow("w");

        Assert.Equal(msg.WindowKey, result?.WindowKey);
    }

    [Fact]
    public void GetFinalizedWindowsBySize_FiltersByWindowMinutes()
    {
        var store = new RocksDbStateStore<string, WindowFinalMessage>("t", new StateStoreOptions { EnableCache = false }, NullLoggerFactory.Instance);
        var consumer = new WindowFinalConsumer(store, NullLoggerFactory.Instance);
        var m1 = new WindowFinalMessage { WindowKey = "w1", WindowMinutes = 5, WindowStart = DateTime.UtcNow, WindowEnd = DateTime.UtcNow.AddMinutes(5), PodId = "p" };
        var m2 = new WindowFinalMessage { WindowKey = "w2", WindowMinutes = 10, WindowStart = DateTime.UtcNow, WindowEnd = DateTime.UtcNow.AddMinutes(10), PodId = "p" };
        store.Put("w1", m1);
        store.Put("w2", m2);

        var list = consumer.GetFinalizedWindowsBySize(5);

        Assert.Single(list);
        Assert.Equal("w1", list[0].WindowKey);
    }
}
