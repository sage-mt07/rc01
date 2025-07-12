using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Core.Modeling;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.ModelBuilderTests;

public class TopicFluentApiTests
{
    private class Order
    {
        public int Id { get; set; }
    }

    [Fact]
    public void FluentApi_ConfiguresTopicSettings()
    {
        var builder = new ModelBuilder();
        builder.Entity<Order>()
            .AsTable("orders")
            .WithPartitions(3)
            .WithReplicationFactor(2)
            .WithPartitioner("custom");

        var model = builder.GetEntityModel<Order>();
        Assert.NotNull(model);
        Assert.Equal("orders", model.TopicName);
    }
}
