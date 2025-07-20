using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Chr.Avro.Confluent;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Application;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Integration;

public class DummyFlagMessageTests
{

    public class OrderValue
    {
        public int CustomerId { get; set; }
        public int Id { get; set; }
        public string? Region { get; set; }
        public double Amount { get; set; }
        public bool IsHighPriority { get; set; }
        public int Count { get; set; }
    }

    public class DummyContext : KsqlContext
    {
        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderValue>().WithTopic("orders");
        }
    }

    // KafkaProducer が is_dummy ヘッダーを追加するか確認
    [KsqlDbFact]
    public async Task SendAsync_AddsDummyFlagHeader()
    {
        await TestEnvironment.ResetAsync();

        var ctx = KsqlContextBuilder.Create()
            .UseSchemaRegistry("http://localhost:8081")
            .BuildContext<DummyContext>();

        var messageContext = new KafkaMessageContext
        {
            Headers = new Dictionary<string, object> { ["is_dummy"] = true }
        };

        await ctx.Set<OrderValue>().AddAsync(new OrderValue
        {
            CustomerId = 1,
            Id = 1,
            Region = "west",
            Amount = 10d,
            IsHighPriority = false,
            Count = 1
        }, messageContext);

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = "localhost:9093",
            GroupId = Guid.NewGuid().ToString(),
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        using var schema = new CachedSchemaRegistryClient(new SchemaRegistryConfig { Url = "http://localhost:8081" });
        using var consumer = new ConsumerBuilder<int, OrderValue>(consumerConfig)
            .SetValueDeserializer(new AsyncSchemaRegistryDeserializer<OrderValue>(schema).AsSyncOverAsync())
            .SetKeyDeserializer(Deserializers.Int32)
            .Build();

        consumer.Subscribe("orders");
        var result = consumer.Consume(TimeSpan.FromSeconds(10));
        Assert.NotNull(result);
        var headerBytes = result.Message.Headers?.GetLastBytes("is_dummy");
        Assert.NotNull(headerBytes);
        Assert.Equal("true", Encoding.UTF8.GetString(headerBytes!));

        await ctx.DisposeAsync();
    }
}
