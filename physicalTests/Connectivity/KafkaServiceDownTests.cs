using Confluent.Kafka;
using System;
using System.Threading;
using System.Threading.Tasks;
using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Configuration;
using Xunit;

using Kafka.Ksql.Linq.Core.Abstractions;
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

public class KafkaServiceDownTests
{
    public class Order
    {
        public int Id { get; set; }
        public double Amount { get; set; }
    }

    public class OrderContext : KsqlContext
    {
        public OrderContext() : base(new KsqlDslOptions()) { }
        public OrderContext(KsqlDslOptions options) : base(options) { }
        protected override void OnModelCreating(IModelBuilder modelBuilder)
            => modelBuilder.Entity<Order>().WithTopic("orders");
    }

    private static KsqlDslOptions CreateOptions() => new()
    {
        Common = new CommonSection { BootstrapServers = TestEnvironment.KafkaBootstrapServers },
        SchemaRegistry = new SchemaRegistrySection { Url = TestEnvironment.SchemaRegistryUrl }
    };

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AddAsync_ShouldThrow_WhenKafkaIsDown()
    {
        if (!KsqlDbAvailability.IsAvailable())
            throw new SkipException(KsqlDbAvailability.SkipReason);

        await TestEnvironment.ResetAsync();
        await DockerHelper.StopServiceAsync("kafka");

        await using var ctx = new OrderContext(CreateOptions());
        var msg = new Order { Id = 1, Amount = 100 };

        var ex = await Assert.ThrowsAsync<KafkaException>(() =>
            ctx.Set<Order>().AddAsync(msg));
        Assert.Contains("refused", ex.Message, StringComparison.OrdinalIgnoreCase);

        await DockerHelper.StartServiceAsync("kafka");
        await TestEnvironment.SetupAsync();

        await ctx.Set<Order>().AddAsync(new Order { Id = 2, Amount = 50 });
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ForeachAsync_ShouldThrow_WhenKafkaIsDown()
    {
        if (!KsqlDbAvailability.IsAvailable())
            throw new SkipException(KsqlDbAvailability.SkipReason);

        await TestEnvironment.ResetAsync();
        await DockerHelper.StopServiceAsync("kafka");

        await using var ctx = new OrderContext(CreateOptions());

        await Assert.ThrowsAsync<ConsumeException>(async () =>
        {
            await ctx.Set<Order>().ForEachAsync(_ => Task.CompletedTask, TimeSpan.FromSeconds(1));
        });

        await DockerHelper.StartServiceAsync("kafka");
        await TestEnvironment.SetupAsync();
    }
}
