using Kafka.Ksql.Linq.StateStore.Core;
using Kafka.Ksql.Linq.StateStore.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.IO;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.StateStore;

public class RocksDbStateStoreTests
{
    private class Sample
    {
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void PutGetDelete_InMemoryStore()
    {
        var options = new StateStoreOptions { EnableCache = false };
        using var store = new RocksDbStateStore<string, Sample>("s", options, new NullLoggerFactory());

        store.Put("a", new Sample { Name = "first" });
        var value = store.Get("a");
        Assert.NotNull(value);
        Assert.Equal("first", value!.Name);

        var removed = store.Delete("a");
        Assert.True(removed);
        Assert.Null(store.Get("a"));
    }

    [Fact]
    public void All_ReturnsStoredItems()
    {
        var options = new StateStoreOptions { EnableCache = false };
        using var store = new RocksDbStateStore<int, Sample>("s", options, new NullLoggerFactory());
        store.Put(1, new Sample { Name = "n1" });
        store.Put(2, new Sample { Name = "n2" });

        var items = store.All();
        Assert.Equal(2, System.Linq.Enumerable.Count(items));
    }

    [Fact]
public void PersistToFile_WhenCacheEnabled()
{
    var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    var options = new StateStoreOptions { EnableCache = true, BaseDirectory = dir };
    // Step 1: 書き込み
    using (var store = new RocksDbStateStore<string, Sample>("cache", options, new NullLoggerFactory()))
    {
        store.Put("k", new Sample { Name = "val" });
        // store.Flush(); // Flushが無効ならむしろ省略
    }

    var allFiles = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
    Assert.NotEmpty(allFiles); // RocksDBが何かしら出力していることを確認

    // load through new instance
    using var store2 = new RocksDbStateStore<string, Sample>("cache", options, new NullLoggerFactory());
    var loaded = store2.Get("k");
    Assert.NotNull(loaded);
    Assert.Equal("val", loaded!.Name);
}
}
