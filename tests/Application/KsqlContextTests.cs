using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Context;
using Kafka.Ksql.Linq.Messaging.Consumers;
using Kafka.Ksql.Linq.Messaging.Producers;
using Kafka.Ksql.Linq.Core.Dlq;
using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq.Cache.Core;
using System;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Application;

public class KsqlContextTests
{
    private class TestContext : KsqlContext
    {
        public TestContext() : base() { }
        public TestContext(KafkaContextOptions opt) : base(opt) { }

        protected override bool SkipSchemaRegistration => true;

        public IEntitySet<T> CallCreateEntitySet<T>(EntityModel model) where T : class
            => base.CreateEntitySet<T>(model);

        public KafkaProducerManager CallGetProducerManager() => base.GetProducerManager();
        public KafkaConsumerManager CallGetConsumerManager() => base.GetConsumerManager();
        public DlqProducer CallGetDlqProducer() => base.GetDlqProducer();
    }

    [Fact]
    public void Constructors_InitializeManagers()
    {
        var ctx = new TestContext();
        Assert.NotNull(ctx.CallGetProducerManager());
        Assert.NotNull(ctx.CallGetConsumerManager());
        Assert.Contains("schema auto-registration supported", ctx.ToString());
    }

    [Fact]
    public void CreateEntitySet_ReturnsEventSet()
    {
        var ctx = new TestContext();
        var model = new EntityModel
        {
            EntityType = typeof(TestEntity),
            TopicName = "test-topic",
            KeyProperties = new[] { typeof(TestEntity).GetProperty(nameof(TestEntity.Id))! },
            AllProperties = typeof(TestEntity).GetProperties()
        };
        var set = ctx.CallCreateEntitySet<TestEntity>(model);
        Assert.IsType<ReadCachedEntitySet<TestEntity>>(set);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var ctx = new TestContext();
        ctx.Dispose();
    }

    [Fact]
    public void GetDlqProducer_ReturnsInstance()
    {
        var ctx = new TestContext();
        Assert.NotNull(ctx.CallGetDlqProducer());
    }
}
