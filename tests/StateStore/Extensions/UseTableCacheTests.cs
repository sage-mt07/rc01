using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.StateStore.Extensions;
using Kafka.Ksql.Linq.StateStore.Management;
using Kafka.Ksql.Linq.StateStore;
using Xunit;
using System.Reflection;

namespace Kafka.Ksql.Linq.Tests.StateStore.Extensions;

public class UseTableCacheTests
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
    public void UseTableCache_CreatesEntityConfigurationsWhenEmpty()
    {
        var ctx = new TestContext();
        var optionsField = typeof(KsqlContext).GetField("_dslOptions", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var options = (KsqlDslOptions)optionsField.GetValue(ctx)!;
        options.Entities.Clear();

        KafkaContextStateStoreExtensions.UseTableCache(ctx, options);

        Assert.Single(options.Entities);
        var config = options.Entities[0];
        Assert.Equal(nameof(Sample), config.Entity);
        Assert.True(config.EnableCache);
        Assert.Equal(StoreTypes.RocksDb, config.StoreType);

        var manager = ctx.GetStateStoreManager();
        Assert.NotNull(manager);
        var store = manager!.GetOrCreateStore<string, object>(typeof(Sample), 0);
        Assert.NotNull(store);
    }
}
