using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Chr.Avro.Confluent;
using Confluent.Kafka.SyncOverAsync;
using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Integration;

public class SchemaNameCaseSensitivityTests
{

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

    // Context using custom serialization for OrderWrongCase
    public class WrongCaseContext : KsqlContext
    {
        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderWrongCase>().WithTopic("orders");
        }

        protected override IEntitySet<T> CreateEntitySet<T>(EntityModel entityModel)
        {
            if (typeof(T) == typeof(OrderWrongCase))
            {
                return (IEntitySet<T>)new ManualSerializeEventSet(this, entityModel);
            }

            return base.CreateEntitySet<T>(entityModel);
        }

        private class ManualSerializeEventSet : EventSet<OrderWrongCase>
        {
            public ManualSerializeEventSet(IKsqlContext context, EntityModel model) : base(context, model) { }

            public override IAsyncEnumerator<OrderWrongCase> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public override Task<List<OrderWrongCase>> ToListAsync(CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            protected override async Task SendEntityAsync(OrderWrongCase entity, CancellationToken cancellationToken)
            {
                var config = new ProducerConfig { BootstrapServers = "localhost:9093" };
                using var schema = new CachedSchemaRegistryClient(new SchemaRegistryConfig { Url = "http://localhost:8081" });
                using var producer = new ProducerBuilder<int, OrderWrongCase>(config)
                    .SetKeySerializer(Serializers.Int32)
                    .SetValueSerializer(new AsyncSchemaRegistrySerializer<OrderWrongCase>(schema).AsSyncOverAsync())
                    .Build();

                var msg = new Message<int, OrderWrongCase>
                {
                    Key = 1,
                    Value = entity,
                    Headers = new Headers { new Header("is_dummy", Encoding.UTF8.GetBytes("true")) }
                };

                await producer.ProduceAsync("orders", msg, cancellationToken);
            }
        }
    }

    private async Task EnsureTablesAsync()
    {
        await using var ctx = TestEnvironment.CreateContext();
        foreach (var ddl in TestSchema.GenerateTableDdls())
            await ctx.ExecuteStatementAsync(ddl);
    }

    private async Task ProduceValidDummyAsync()
    {
        var ctx = KsqlContextBuilder.Create()
            .UseSchemaRegistry("http://localhost:8081")
            .BuildContext<OrderContext>();

        var dummyCtx = new KafkaMessageContext
        {
            Headers = new Dictionary<string, object> { ["is_dummy"] = true }
        };

        await ctx.Set<OrderCorrectCase>().AddAsync(new OrderCorrectCase
        {
            CustomerId = 1,
            Id = 1,
            Region = "east",
            Amount = 10d
        }, dummyCtx);

        await Task.Delay(500);
        await ctx.DisposeAsync();
    }

    // スキーマ定義と異なるフィールド名の大文字小文字違いを送信した場合に例外が発生するか確認
    [KsqlDbFact]
    [Trait("Category", "Integration")]
    public async Task MismatchedFieldCase_ShouldThrowException()
    {
        await TestEnvironment.ResetAsync();

        await EnsureTablesAsync();
        await ProduceValidDummyAsync();

        var ctx = KsqlContextBuilder.Create()
            .UseSchemaRegistry("http://localhost:8081")
            .BuildContext<WrongCaseContext>();

        var set = ctx.Set<OrderWrongCase>();

        await Assert.ThrowsAsync<SchemaRegistryException>(() =>
            set.AddAsync(new OrderWrongCase
            {
                CustomerId = 1,
                Id = 1,
                region = "west",
                Amount = 5d
            }));

        await ctx.DisposeAsync();
    }
}
