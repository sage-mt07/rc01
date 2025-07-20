using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Chr.Avro.Confluent;
using Confluent.Kafka.SyncOverAsync;
using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Messaging.Producers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Integration;

public class SchemaNameCaseSensitivityTests
{
    private readonly IKsqlClient _client = new KsqlClient(new Uri("http://localhost:8088"));

    public class OrderCorrectCase
    {
        public int CustomerId { get; set; }
        public int Id { get; set; }
        public string Region { get; set; } = string.Empty;
        public double Amount { get; set; }
    }

    public class OrderWrongCase
    {
        public int CustomerId { get; set; }
        public int Id { get; set; }
        public string region { get; set; } = string.Empty; // lowercase r
        public double Amount { get; set; }
    }

    public class OrderContext : KsqlContext
    {
        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderCorrectCase>().WithTopic("orders");
        }
    }

    private async Task EnsureTablesAsync()
    {
        foreach (var ddl in TestSchema.GenerateTableDdls())
            await _client.ExecuteStatementAsync(ddl);
    }

    private async Task ProduceValidDummyAsync()
    {
        var ctx = KsqlContextBuilder.Create()
            .UseSchemaRegistry("http://localhost:8081")
            .BuildContext<OrderContext>();

        var manager = Kafka.Ksql.Linq.Tests.PrivateAccessor.InvokePrivate<KafkaProducerManager>(ctx, "GetProducerManager", Type.EmptyTypes);
        var dummyCtx = new KafkaMessageContext
        {
            Headers = new Dictionary<string, object> { ["is_dummy"] = true }
        };

        await (await manager.GetProducerAsync<OrderCorrectCase>()).SendAsync(new OrderCorrectCase
        {
            CustomerId = 1,
            Id = 1,
            Region = "east",
            Amount = 10d
        }, dummyCtx);

        await Task.Delay(500);
        await ctx.DisposeAsync();
    }

    [KsqlDbFact]
    [Trait("Category", "Integration")]
    public async Task MismatchedFieldCase_ShouldThrowException()
    {
        await TestEnvironment.ResetAsync();

        await EnsureTablesAsync();
        await ProduceValidDummyAsync();

        var config = new ProducerConfig { BootstrapServers = "localhost:9093" };
        using var schema = new CachedSchemaRegistryClient(new SchemaRegistryConfig { Url = "http://localhost:8081" });
        using var producer = new ProducerBuilder<int, OrderWrongCase>(config)
            .SetKeySerializer(Serializers.Int32)
            .SetValueSerializer(new AsyncSchemaRegistrySerializer<OrderWrongCase>(schema).AsSyncOverAsync())
            .Build();

        var msg = new Message<int, OrderWrongCase>
        {
            Key = 1,
            Value = new OrderWrongCase { CustomerId = 1, Id = 1, region = "west", Amount = 5d },
            Headers = new Headers { new Header("is_dummy", Encoding.UTF8.GetBytes("true")) }
        };

        await Assert.ThrowsAsync<SchemaRegistryException>(() => producer.ProduceAsync("orders", msg));
    }
}
