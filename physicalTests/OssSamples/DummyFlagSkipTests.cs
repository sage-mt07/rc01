using Confluent.Kafka;
using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Integration;

public class DummyFlagSkipTests
{
    public class Order
    {
        public int Id { get; set; }
        public string Region { get; set; } = string.Empty;
        public double Amount { get; set; }
        public int Count { get; set; }
    }

    public class DummyContext : KsqlContext
    {
        public DummyContext() : base(new KsqlDslOptions()) { }
        public DummyContext(KsqlDslOptions options) : base(options) { }
        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>().WithTopic("orders");
        }
    }

    [KsqlDbFact]
    [Trait("Category", "Integration")]
    public async Task Consumer_IgnoresDummyMessages()
    {
        await TestEnvironment.ResetAsync();

        var options = new KsqlDslOptions
        {
            Common = new CommonSection { BootstrapServers = "localhost:9092" },
            SchemaRegistry = new SchemaRegistrySection { Url = "http://localhost:8088" }
        };

        await using var ctx = new DummyContext(options);

        var headers = new Dictionary<string, string> { ["is_dummy"] = "true" };
        await ctx.Set<Order>().AddAsync(new Order { Id = 1, Region = "east", Amount = 1, Count = 1 }, headers);
        await ctx.Set<Order>().AddAsync(new Order { Id = 2, Region = "east", Amount = 2, Count = 1 });

        var builder = ctx.CreateConsumerBuilder<Order>();
        using var consumer = builder.SetErrorHandler((_, _) => { }).Build();
        consumer.Subscribe(ctx.GetTopicName<Order>());

        var received = new List<Order>();
        var end = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < end && received.Count < 1)
        {
            var msg = consumer.Consume(TimeSpan.FromSeconds(1));
            if (msg == null) continue;
            var dummyHeader = msg.Message.Headers?.GetLastBytes("is_dummy");
            var isDummy = dummyHeader != null && Encoding.UTF8.GetString(dummyHeader) == "true";
            if (!isDummy)
                received.Add(msg.Message.Value);
        }
        consumer.Close();

        Assert.Single(received);
        Assert.Equal(2, received[0].Id);
    }
}
