using Confluent.Kafka;
using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Configuration;
using Kafka.Ksql.Linq.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace Kafka.Ksql.Linq.Tests.Integration;

internal static class KsqlDbAvailability
{
    public const string SkipReason = "Skipped in CI due to missing ksqlDB instance or schema setup failure";
    private static bool _available;
    private static DateTime? _lastFailure;
    private static readonly object _sync = new();

    public static bool IsAvailable()
    {
        lock (_sync)
        {
            if (_available)
                return true;

            if (_lastFailure.HasValue && DateTime.UtcNow - _lastFailure.Value < TimeSpan.FromSeconds(5))
                return false;

            const int attempts = 3;
            for (var i = 0; i < attempts; i++)
            {
                try
                {
                    using var ctx = TestEnvironment.CreateContext();
                    var r = ctx.ExecuteStatementAsync("SHOW TOPICS;").GetAwaiter().GetResult();
                    if (r.IsSuccess)
                    {
                        _available = true;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ksqlDB check attempt {i + 1} failed: {ex.Message}");
                }

                Thread.Sleep(1000);
            }

            _available = false;
            _lastFailure = DateTime.UtcNow;
            return false;
        }
    }
}

public class DlqIntegrationTests
{
    public class Order
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
    }

    public class OrderContext : KsqlContext
    {
        public OrderContext() : base(new KsqlDslOptions()) { }
        public OrderContext(KsqlDslOptions options) : base(options) { }
        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>()
                .WithTopic("orders")
                .WithDecimalPrecision(o => o.Amount, precision: 18, scale: 2);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task FailingForEach_SendsToDlq()
    {
        if (!KsqlDbAvailability.IsAvailable())
            throw new SkipException(KsqlDbAvailability.SkipReason);

        await TestEnvironment.ResetAsync();

        var options = new KsqlDslOptions
        {
            Common = new CommonSection { BootstrapServers = TestEnvironment.KafkaBootstrapServers },
            SchemaRegistry = new SchemaRegistrySection { Url = TestEnvironment.SchemaRegistryUrl }
        };

        await using var ctx = new OrderContext(options);

        // send invalid raw message to trigger deserialization failure
        var conf = new ProducerConfig { BootstrapServers = TestEnvironment.KafkaBootstrapServers };
        using (var producer = new ProducerBuilder<Null, string>(conf).Build())
        {
            await producer.ProduceAsync("orders", new Message<Null, string> { Value = "bad" });
            producer.Flush(TimeSpan.FromSeconds(5));
        }

        // consuming with typed context will cause DLQ forwarding
        await ctx.Set<Order>().ToListAsync();

        var builder = ctx.CreateConsumerBuilder<DlqEnvelope>();
        using var consumer = builder
            .SetErrorHandler((_, _) => { })
            .Build();
        consumer.Subscribe(ctx.GetDlqTopicName());
        var dlqMsg = consumer.Consume(TimeSpan.FromSeconds(10));
        consumer.Close();

        Assert.NotNull(dlqMsg);
        Assert.NotEmpty(dlqMsg.Message.Value.ErrorType);
    }
}
