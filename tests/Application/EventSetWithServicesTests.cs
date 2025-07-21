using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Context;
using Kafka.Ksql.Linq;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Application;

public class EventSetWithServicesTests
{
    private class TestContext : KsqlContext
    {
        public TestContext() : base(new KafkaContextOptions()) { }

        protected override bool SkipSchemaRegistration => true;
    }

    [Fact]
    public void Constructors_CreateInstances()
    {
        var ctx = new TestContext();
        var model = new EntityModel
        {
            EntityType = typeof(TestEntity),
            TopicName = "t",
            AllProperties = typeof(TestEntity).GetProperties(),
            KeyProperties = new[] { typeof(TestEntity).GetProperty(nameof(TestEntity.Id))! }
        };
        var set = new EventSetWithServices<TestEntity>(ctx, model);
        Assert.NotNull(set);
    }
}
