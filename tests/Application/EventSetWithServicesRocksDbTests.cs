using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Cache.Core;
using Kafka.Ksql.Linq.Cache.Extensions;
using Kafka.Ksql.Linq.Cache;
using System.Collections.Concurrent;
using Xunit;
using System.Collections.Generic;
using System.Reflection;

namespace Kafka.Ksql.Linq.Tests.Application;

public class EventSetWithServicesRocksDbTests
{
    private class TestContext : KsqlContext
    {
        public TestContext() : base(new KsqlDslOptions()) { }

        protected override bool SkipSchemaRegistration => true;

        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Sample>().AsTable();
        }
    }

    private class Sample
    {
        public int Id { get; set; }
    }

    [Fact(Skip="Cache not initialized")]
    public void ToListAsync_UsesCacheWhenConfigured()
    {
        var ctx = new TestContext();

        var optionsField = typeof(KsqlContext).GetField("_dslOptions", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var options = (KsqlDslOptions)optionsField.GetValue(ctx)!;
        options.Entities.Add(new EntityConfiguration { Entity = nameof(Sample), SourceTopic = "s", EnableCache = true });

        KsqlContextCacheExtensions.UseTableCache(ctx, options);
        var registry = ctx.GetTableCacheRegistry()!;
        var cache = registry.GetCache<Sample>() as RocksDbTableCache<Sample>;
        Assert.NotNull(cache);
        var storeField = typeof(RocksDbTableCache<Sample>).GetField("_store", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var dict = (ConcurrentDictionary<string, Sample>)storeField.GetValue(cache!)!;
        dict["1"] = new Sample { Id = 1 };

        var model = ctx.GetEntityModels()[typeof(Sample)];
        var set = new ReadCachedEntitySet<Sample>(ctx, model);
        var list = set.ToListAsync().GetAwaiter().GetResult();

        Assert.Single(list);
        Assert.Equal(1, list[0].Id);
    }
}
