using Kafka.Ksql.Linq.Core.Modeling;
using Kafka.Ksql.Linq.Core.Abstractions;

namespace Samples.TopicFluentApiExtension;

// シンプルなトピック名とパーティション設定の例
public static class Example1_Basic
{
    private class Order
    {
        public int Id { get; set; }
    }

    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<Order>()
            .HasKey(o => o.Id)
            .AsTable("orders")
            .WithPartitions(3);
    }
}
