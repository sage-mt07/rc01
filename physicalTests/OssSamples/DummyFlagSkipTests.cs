using Confluent.Kafka;
using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
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

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Consumer_IgnoresDummyMessages()
    {
        if (!KsqlDbAvailability.IsAvailable())
            throw new SkipException(KsqlDbAvailability.SkipReason);

        await TestEnvironment.ResetAsync();

        var options = new KsqlDslOptions
        {
            Common = new CommonSection { BootstrapServers = TestEnvironment.KafkaBootstrapServers },
            SchemaRegistry = new SchemaRegistrySection { Url = TestEnvironment.SchemaRegistryUrl }
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
