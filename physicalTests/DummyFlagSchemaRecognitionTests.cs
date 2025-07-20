using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Chr.Avro.Confluent;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Application;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Integration;

public class DummyFlagSchemaRecognitionTests
{

    public class OrderValue
    {
        public int CustomerId { get; set; }
        public int Id { get; set; }
        public string Region { get; set; } = string.Empty;
        public double Amount { get; set; }
        public bool IsHighPriority { get; set; }
        public int Count { get; set; }
    }

    public class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class EventLog
    {
        public int Level { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class NullableOrder
    {
        public int? CustomerId { get; set; }
        public string Region { get; set; } = string.Empty;
        public double Amount { get; set; }
    }

    public class NullableKeyOrder
    {
        public int? CustomerId { get; set; }
        public double Amount { get; set; }
    }

    public class DummyContext : KsqlContext
    {
        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderValue>().WithTopic("orders");
            modelBuilder.Entity<Customer>().WithTopic("customers");
            modelBuilder.Entity<EventLog>().WithTopic("events");
            modelBuilder.Entity<NullableOrder>().WithTopic("orders_nullable");
            modelBuilder.Entity<NullableKeyOrder>().WithTopic("orders_nullable_key");
        }
    }

    private async Task ProduceDummyRecordsAsync()
    {
        var ctx = KsqlContextBuilder.Create()
            .UseSchemaRegistry("http://localhost:8081")
            .BuildContext<DummyContext>();

        var dummyCtx = new KafkaMessageContext
        {
            Headers = new Dictionary<string, object> { ["is_dummy"] = true }
        };

        await ctx.Set<OrderValue>().AddAsync(new OrderValue
        {
            CustomerId = 1,
            Id = 1,
            Region = "east",
            Amount = 10d,
            IsHighPriority = false,
            Count = 1
        }, dummyCtx);
        await ctx.Set<Customer>().AddAsync(new Customer { Id = 1, Name = "alice" }, dummyCtx);
        await ctx.Set<EventLog>().AddAsync(new EventLog { Level = 1, Message = "init" }, dummyCtx);
        await ctx.Set<NullableOrder>().AddAsync(new NullableOrder { CustomerId = 1, Region = "east", Amount = 10d }, dummyCtx);
        await ctx.Set<NullableKeyOrder>().AddAsync(new NullableKeyOrder { CustomerId = 1, Amount = 10d }, dummyCtx);

        await Task.Delay(500);
        await ctx.DisposeAsync();
    }

    [KsqlDbFact]
    [Trait("Category", "Integration")]
    public async Task DummyMessages_EnableQueries()
    {
        await TestEnvironment.ResetAsync();

        await using (var ctx = TestEnvironment.CreateContext())
        {
            foreach (var ddl in TestSchema.GenerateTableDdls())
            {
                await ctx.ExecuteStatementAsync(ddl);
            }
        }

        await ProduceDummyRecordsAsync();
        await Task.Delay(2000);

        var queries = new[]
        {
            "SELECT * FROM ORDERS EMIT CHANGES LIMIT 1;",
            "SELECT * FROM CUSTOMERS EMIT CHANGES LIMIT 1;",
            "SELECT COUNT(*) FROM EVENTS;",
            "SELECT REGION, COUNT(*) FROM ORDERS GROUP BY REGION EMIT CHANGES LIMIT 1;"
        };

        await using (var ctx = TestEnvironment.CreateContext())
        {
            foreach (var q in queries)
            {
                var r = await ctx.ExecuteExplainAsync(q);
                Assert.True(r.IsSuccess, $"{q} failed: {r.Message}");
            }
        }
    }

    [KsqlDbFact]
    [Trait("Category", "Integration")]
    public async Task Consumer_SkipsDummyMessages()
    {
        await TestEnvironment.ResetAsync();

        await ProduceDummyRecordsAsync();

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
        var isDummy = Encoding.UTF8.GetString(headerBytes!) == "true";

        var records = new List<OrderValue>();
        if (!isDummy)
            records.Add(result.Message.Value);

        Assert.Empty(records);
    }
}
