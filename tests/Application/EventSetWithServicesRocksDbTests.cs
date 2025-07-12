using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.StateStore.Extensions;
using Kafka.Ksql.Linq.StateStore.Management;
using Kafka.Ksql.Linq.StateStore;
using Xunit;
using System.Collections.Generic;
using System.Reflection;

namespace Kafka.Ksql.Linq.Tests.Application;

public class EventSetWithServicesRocksDbTests
{
    private class TestContext : KsqlContext
    {
        public TestContext() : base() { }

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

    [Fact]
    public void ToListAsync_UsesStateStoreWhenConfigured()
    {
        var ctx = new TestContext();

        var optionsField = typeof(KsqlContext).GetField("_dslOptions", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var options = (KsqlDslOptions)optionsField.GetValue(ctx)!;
        options.Entities.Add(new EntityConfiguration { Entity = nameof(Sample), SourceTopic = "s", StoreType = StoreTypes.RocksDb });

        KafkaContextStateStoreExtensions.InitializeStateStores(ctx, options);
        var manager = ctx.GetStateStoreManager()!;
        var store = manager.GetOrCreateStore<string, Sample>(typeof(Sample), 0);
        store.Put("1", new Sample { Id = 1 });

        var model = ctx.GetEntityModels()[typeof(Sample)];
        var set = new EventSetWithServices<Sample>(ctx, model);
        var list = set.ToListAsync().GetAwaiter().GetResult();

        Assert.Single(list);
        Assert.Equal(1, list[0].Id);
    }
}
